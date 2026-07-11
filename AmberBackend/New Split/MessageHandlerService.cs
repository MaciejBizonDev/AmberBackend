using AmberBackend.Combat;
using AmberBackend.Inventory;
using AmberBackend.Movement;
using AmberBackend.Zones;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static MovementService;

public class BaseMessage { public string type; }

public class LoginRequestMessage : BaseMessage
{
    public string username;
    public string password;
}

public class PositionUpdateMessage : BaseMessage
{
    public string playerId;
    public int x;
    public int y;
}

public class TurnRequestMessage : BaseMessage
{
    public Direction direction;
}

public class PathRequestMessage : BaseMessage
{
    public string playerId;
    public int targetX;
    public int targetY;
}

public class StateSnapshotMessage
{
    public string type { get; set; } = "state_snapshot";
    public List<EntityStateDto> entities { get; set; }
}

public class UseAbilityMessage
{
    public string type;
    public string abilityId;
    public string targetId;
}

public class MessageHandlerService
{
    private readonly PlayerService _playerService;
    private readonly PlayerSessionManager _sessionManager;
    private readonly ZoneManager _zoneManager;
    private readonly ZoneTransitionService _zoneTransitionService;
    private readonly InventoryService _inventoryService;
    private readonly RegistrationService _registrationService;

    private readonly Dictionary<string, Func<WebSocket, string, string, Task>> _handlers;
    private readonly Dictionary<string, Func<WebSocket, string, Task<string>>> _registrationHandlers;

    public MessageHandlerService(
        PlayerService playerService,
        PlayerSessionManager sessionManager,
        ZoneManager zoneManager,
        ZoneTransitionService zoneTransitionService,
        InventoryService inventoryService,
        RegistrationService registrationService)
    {
        _playerService = playerService;
        _sessionManager = sessionManager;
        _zoneManager = zoneManager;
        _zoneTransitionService = zoneTransitionService;
        _inventoryService = inventoryService;
        _registrationService = registrationService;

        _registrationHandlers = new Dictionary<string, Func<WebSocket, string, Task<string>>>
        {
            { "login_request", HandleLoginRequest },
            { "register_request", HandleRegisterRequest }
        };

        _handlers = new Dictionary<string, Func<WebSocket, string, string, Task>>
        {
            { "position_update", HandlePositionUpdate },
            { "path_request", HandlePathRequest },
            { "state_request", HandleStateRequest },
            { "use_ability", HandleUseAbility },
            { "inventory_request", HandleInventoryRequest },
            { "use_item", HandleUseItem },
            { "merchant_open", HandleMerchantOpen },
            { "merchant_purchase", HandleMerchantPurchase },
            { "walkability_update_request", HandleWalkabilityUpdateRequest },
            { "turn_request", HandleTurnRequest }
        };
    }

    public async Task<string> HandleMessageAsync(WebSocket ws, string type, string message, string currentPlayerId)
    {
        if (_registrationHandlers.TryGetValue(type, out var reg))
            return await reg(ws, message);

        if (_handlers.TryGetValue(type, out var handler))
        {
            await handler(ws, message, currentPlayerId);
            return currentPlayerId;
        }

        Console.WriteLine($"Unknown message type: {type}");
        return currentPlayerId;
    }

    private async Task<string> HandleLoginRequest(WebSocket ws, string message)
    {
        var request = JsonConvert.DeserializeObject<LoginRequestMessage>(message);
        var playerData = _playerService.Login(request.username, request.password);

        if (playerData != null)
        {
            var playerId = playerData.PlayerId;
            _sessionManager.RegisterSession(playerId, ws, request.username);

            string zoneId = playerData.CurrentZone ?? "test_zone";
            var zone = _zoneManager.GetZone(zoneId);

            if (zone == null)
            {
                Console.WriteLine($"[MessageHandler] Zone {zoneId} not found! Defaulting to test_zone");
                zoneId = "test_zone";
                zone = _zoneManager.GetZone(zoneId);
            }

            var spawnPos = new TilePosition(playerData.LastX, playerData.LastY);
            zone.AddPlayer(playerId, spawnPos);
            _sessionManager.SetPlayerZone(playerId, zoneId);

            // Get walkability data
            var walkabilityData = zone.MovementService.GetWalkabilityData();

            var response = new
            {
                type = "login_response",
                success = true,
                playerId = playerId,
                x = playerData.LastX,
                y = playerData.LastY,
                zoneId = zoneId,
                walkability = new
                {
                    minX = walkabilityData.MinX,
                    minY = walkabilityData.MinY,
                    maxX = walkabilityData.MaxX,
                    maxY = walkabilityData.MaxY,
                    walkableTiles = walkabilityData.WalkableTiles.Select(t => new { x = t.X, y = t.Y })
                }
            };

            var json = JsonConvert.SerializeObject(response);
            var buf = Encoding.UTF8.GetBytes(json);
            await ws.SendAsync(buf, WebSocketMessageType.Text, true, CancellationToken.None);

            Console.WriteLine($"[MessageHandler] Player logged in: {request.username} ({playerId}) -> Zone: {zoneId}");

            // NEW: Send state snapshot with all entities in zone
            var snapshot = zone.GetSnapshot();
            var snapshotMessage = new
            {
                type = "state_snapshot",
                entities = snapshot
            };

            var snapshotJson = JsonConvert.SerializeObject(snapshotMessage);
            var snapshotBuf = Encoding.UTF8.GetBytes(snapshotJson);
            await ws.SendAsync(snapshotBuf, WebSocketMessageType.Text, true, CancellationToken.None);

            Console.WriteLine($"[MessageHandler] Sent state snapshot to {playerId}: {snapshot.Count} entities");

            return playerId;
        }
        else
        {
            var response = new
            {
                type = "login_response",
                success = false,
                reason = "Invalid username or password"
            };

            var json = JsonConvert.SerializeObject(response);
            var buf = Encoding.UTF8.GetBytes(json);
            await ws.SendAsync(buf, WebSocketMessageType.Text, true, CancellationToken.None);

            Console.WriteLine($"[MessageHandler] Login failed: {request.username}");
            return null;
        }
    }

    private async Task HandlePositionUpdate(WebSocket ws, string message, string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return;

        var session = _sessionManager.GetSession(playerId);
        if (session == null) return;

        var zone = _zoneManager.GetZone(session.CurrentZoneId);
        if (zone == null) return;

        var update = JsonConvert.DeserializeObject<PositionUpdateMessage>(message);
        if (update == null) return;

        var newPosition = new TilePosition { X = update.x, Y = update.y };
        zone.MovementService.OnPositionUpdate(playerId, newPosition);

        var portal = zone.CheckForPortal(newPosition);
        if (portal != null)
        {
            Console.WriteLine($"[MessageHandler] Player {playerId} triggered portal {portal.PortalId}");
            _zoneTransitionService.TransitionPlayer(playerId, portal);
        }

        await Task.CompletedTask;
    }

    private async Task HandlePathRequest(WebSocket ws, string message, string playerId)
    {
        await Task.CompletedTask;
    }

    private async Task HandleStateRequest(WebSocket ws, string message, string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return;

        var session = _sessionManager.GetSession(playerId);
        if (session == null) return;

        var zone = _zoneManager.GetZone(session.CurrentZoneId);
        if (zone == null) return;

        var snap = zone.GetSnapshot();

        var response = new StateSnapshotMessage
        {
            type = "state_snapshot",
            entities = snap
        };

        var json = JsonConvert.SerializeObject(response);
        var buffer = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task HandleUseAbility(WebSocket ws, string message, string playerId)
    {
        Console.WriteLine($"[MessageHandler] ===== USE ABILITY REQUEST =====");
        Console.WriteLine($"[MessageHandler] Player: {playerId}");
        Console.WriteLine($"[MessageHandler] Raw message: {message}");

        if (string.IsNullOrEmpty(playerId))
        {
            Console.WriteLine("[MessageHandler] ERROR: No playerId!");
            return;
        }

        var request = JsonConvert.DeserializeObject<UseAbilityMessage>(message);

        Console.WriteLine($"[MessageHandler] Ability: {request.abilityId}");
        Console.WriteLine($"[MessageHandler] Target: {request.targetId}");

        var session = _sessionManager.GetSession(playerId);
        if (session == null)
        {
            Console.WriteLine($"[MessageHandler] ERROR: Session not found for {playerId}");
            return;
        }

        var zone = _zoneManager.GetZone(session.CurrentZoneId);
        if (zone == null)
        {
            Console.WriteLine($"[MessageHandler] ERROR: Zone not found: {session.CurrentZoneId}");
            return;
        }

        Console.WriteLine($"[MessageHandler] Zone: {session.CurrentZoneId}");

        var sourcePos = zone.MovementService.GetEntityPosition(playerId);
        var targetPos = zone.MovementService.GetEntityPosition(request.targetId);

        if (sourcePos == null)
        {
            Console.WriteLine($"[MessageHandler] ERROR: Source position not found");
            return;
        }

        if (targetPos == null)
        {
            Console.WriteLine($"[MessageHandler] ERROR: Target position not found");
            return;
        }

        Console.WriteLine($"[MessageHandler] Source pos: ({sourcePos.X}, {sourcePos.Y})");
        Console.WriteLine($"[MessageHandler] Target pos: ({targetPos.X}, {targetPos.Y})");
        Console.WriteLine($"[MessageHandler] Calling CombatService.UseAbility...");

        zone.CombatService.UseAbility(playerId, request.abilityId, request.targetId, sourcePos, targetPos);

        Console.WriteLine($"[MessageHandler] ===== ABILITY REQUEST COMPLETE =====");

        await Task.CompletedTask;
    }

    private async Task HandleWalkabilityUpdateRequest(WebSocket ws, string message, string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return;

        var session = _sessionManager.GetSession(playerId);
        if (session == null) return;

        var zone = _zoneManager.GetZone(session.CurrentZoneId);
        if (zone == null) return;

        var walkabilityData = zone.MovementService.GetWalkabilityData();

        var response = new
        {
            type = "walkability_full_update",
            minX = walkabilityData.MinX,
            minY = walkabilityData.MinY,
            maxX = walkabilityData.MaxX,
            maxY = walkabilityData.MaxY,
            walkableTiles = walkabilityData.WalkableTiles.Select(t => new { x = t.X, y = t.Y })
        };

        var json = JsonConvert.SerializeObject(response);
        var buf = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(buf, WebSocketMessageType.Text, true, CancellationToken.None);

        Console.WriteLine($"[MessageHandler] Sent walkability update to {playerId}");
        await Task.CompletedTask;
    }

    // NEW: Inventory handlers
    private async Task HandleInventoryRequest(WebSocket ws, string message, string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return;

        var inventory = _inventoryService.LoadInventory(playerId);
        var currency = _inventoryService.GetCurrency(playerId);

        var response = new
        {
            type = "inventory_data",
            inventorySize = 50,
            currency = new
            {
                copper = currency.Copper,
                silver = currency.Silver,
                gold = currency.Gold
            },
            items = inventory.Select(i => new
            {
                inventoryId = i.InventoryId,
                itemId = i.ItemId,
                name = i.Definition?.Name,
                description = i.Definition?.Description,
                iconPath = i.Definition?.IconPath,
                quantity = i.Quantity,
                slotIndex = i.SlotIndex,
                maxStackSize = i.Definition?.MaxStackSize ?? 1
            })
        };

        var json = JsonConvert.SerializeObject(response);
        var buf = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(buf, WebSocketMessageType.Text, true, CancellationToken.None);

        Console.WriteLine($"[MessageHandler] Sent inventory to {playerId}");
    }

    private async Task HandleUseItem(WebSocket ws, string message, string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return;

        var request = JsonConvert.DeserializeObject<UseItemRequest>(message);

        // TODO: Implement item usage (consumables, equipment)
        Console.WriteLine($"[MessageHandler] {playerId} used item {request.itemId}");

        await Task.CompletedTask;
    }

    private async Task HandleMerchantOpen(WebSocket ws, string message, string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return;

        var request = JsonConvert.DeserializeObject<MerchantOpenRequest>(message);

        // Get merchant inventory
        var merchantItems = _inventoryService.GetMerchantInventory(request.merchantId);

        var response = new
        {
            type = "merchant_data",
            merchantId = request.merchantId,
            merchantName = "Traveling Merchant", // TODO: Get from NPC data
            items = merchantItems.Select(m => new
            {
                itemId = m.ItemId,
                name = m.Definition?.Name,
                description = m.Definition?.Description,
                iconPath = m.Definition?.IconPath,
                priceCopper = m.Definition?.BuyPrice?.ToCopper() ?? 0,
                stock = m.Stock,
                maxStackSize = m.Definition?.MaxStackSize ?? 1
            })
        };

        var json = JsonConvert.SerializeObject(response);
        var buf = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(buf, WebSocketMessageType.Text, true, CancellationToken.None);

        Console.WriteLine($"[MessageHandler] Sent merchant data to {playerId}: {request.merchantId}");
    }

    private async Task HandleMerchantPurchase(WebSocket ws, string message, string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return;

        var request = JsonConvert.DeserializeObject<MerchantPurchaseRequest>(message);

        bool success = _inventoryService.PurchaseItem(
            playerId,
            request.merchantId,
            request.itemId,
            request.quantity
        );

        // Send updated currency back
        var currency = _inventoryService.GetCurrency(playerId);

        var response = new
        {
            type = "purchase_result",
            success = success,
            reason = success ? "Purchase successful" : "Purchase failed",
            currency = new
            {
                copper = currency.Copper,
                silver = currency.Silver,
                gold = currency.Gold
            }
        };

        var json = JsonConvert.SerializeObject(response);
        var buf = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(buf, WebSocketMessageType.Text, true, CancellationToken.None);

        Console.WriteLine($"[MessageHandler] Purchase {(success ? "succeeded" : "failed")}: {playerId} buying {request.quantity}x {request.itemId}");

        await Task.CompletedTask;
    }

    private async Task<string> HandleRegisterRequest(WebSocket ws, string message)
    {
        var request = JsonConvert.DeserializeObject<LoginRequestMessage>(message); // Reuse LoginRequestMessage

        var (success, regMessage) = await _registrationService.RegisterUser(request.username, request.password);

        var response = new
        {
            type = "register_response",
            success = success,
            message = regMessage
        };

        var json = JsonConvert.SerializeObject(response);
        var buf = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(buf, WebSocketMessageType.Text, true, CancellationToken.None);

        Console.WriteLine($"[MessageHandler] Registration attempt: {request.username} - {(success ? "SUCCESS" : "FAILED")}");

        return null; // Registration doesn't log in automatically
    }

    private async Task HandleTurnRequest(WebSocket ws, string message, string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return;

        var session = _sessionManager.GetSession(playerId);
        if (session == null) return;

        var zone = _zoneManager.GetZone(session.CurrentZoneId);
        if (zone == null) return;

        var request = JsonConvert.DeserializeObject<TurnRequestMessage>(message);

        // Update facing direction - MovementService will handle broadcasting
        zone.MovementService.SetEntityFacing(playerId, request.direction);
        zone.MovementService.BroadcastEntityTurn(playerId, request.direction);

        Console.WriteLine($"[MessageHandler] {playerId} turned to face {request.direction}");
        await Task.CompletedTask;
    }
}

// NEW: Message types
[System.Serializable]
public class UseItemRequest
{
    public string type;
    public string itemId;
}

[System.Serializable]
public class MerchantOpenRequest
{
    public string type;
    public string merchantId;
}

[System.Serializable]
public class MerchantPurchaseRequest
{
    public string type;
    public string merchantId;
    public string itemId;
    public int quantity;
}