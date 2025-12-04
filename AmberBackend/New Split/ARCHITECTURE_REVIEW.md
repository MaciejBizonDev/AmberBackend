# Architecture Review: New Split Folder

## Overview
The "New Split" folder implements a WebSocket server for managing entity movement in a Unity client-server architecture. This document provides a comprehensive code review and feedback.

---

## ✅ Strengths

### 1. **Clean Separation of Concerns**
- **MovementService**: Manages entity movement state and simulation
- **PlayerService**: Handles player registration and tracking
- **MovementWebSocketHandler**: Bridges WebSocket and movement logic
- **MessageHandlerService**: Centralized message routing
- **ServerTicker**: Isolated game loop logic

### 2. **Thread Safety**
- Uses `ConcurrentDictionary` for entity storage
- Appropriate use of thread-safe collections

### 3. **Architecture Pattern**
- Clear separation between network (WebSocket), business logic (MovementService), and game loop

---

## 🔴 Critical Issues Fixed

### 1. **Duplicate Tick Loops** ✅ FIXED
**Problem:** Program.cs had both `ServerTicker.RunAsync` AND `RunGameLoopAsync` - causing movement to tick twice.

**Solution:** Removed the redundant `RunGameLoopAsync` method.

### 2. **WebSocket Error Handling** ✅ FIXED
**Problem:** 
- Fire-and-forget tasks without error handling
- No exception handling for WebSocket operations
- Client disconnects weren't properly handled

**Solution:**
- Added try-catch blocks for WebSocket operations
- Proper cleanup in finally block
- Handles `WebSocketException` for disconnects

### 3. **Thread Safety Issues** ✅ FIXED
**Problem:** Modifying `EntityMovementState` without re-saving to dictionary caused synchronization issues.

**Solution:** Added explicit dictionary updates after state modifications.

### 4. **Missing Player Cleanup** ✅ PARTIALLY FIXED
**Problem:** No mechanism to remove players when they disconnect.

**Solution:** Added `RemoveEntity` method. **TODO:** Call this when WebSocket disconnects.

---

## ⚠️  Remaining Issues & Recommendations

### 1. **Player Cleanup on Disconnect**
**Status:** Mechanism added, but not wired up

**Recommendation:**
```csharp
// In WebSocketServer.HandleClientAsync finally block:
finally
{
    // Cleanup player on disconnect
    if (!string.IsNullOrEmpty(currentPlayerId))
    {
        // TODO: Get references to services and remove player
        // movement.RemoveEntity(currentPlayerId);
    }
    if (ws.State != WebSocketState.Closed)
    {
        try
        {
            await ws.CloseAsync(WebSocketCloseStatus.InternalServerError, "Connection closed", CancellationToken.None);
        }
        catch { }
    }
}
```

### 2. **Configuration**
**Issue:** Hardcoded spawn position (X: 5, Y: -5) in `MessageHandlerService.cs`

**Recommendation:**
- Move to appsettings.json
- Add configuration class for server settings

```json
{
  "GameSettings": {
    "SpawnPosition": {
      "X": 5,
      "Y": -5
    },
    "DefaultSpeed": 6.0,
    "TicksPerSecond": 20
  }
}
```

### 3. **Message Protocol**
**Issue:** Assumes all messages follow `TileClickMessage` structure

**Current:**
```csharp
var baseMsg = JsonConvert.DeserializeObject<TileClickMessage>(msg);
```

**Recommendation:** Use a proper base message class:
```csharp
var baseMsg = JsonConvert.DeserializeObject<BaseMessage>(msg);
```

### 4. **Path Validation**
**Issue:** No validation that paths start from current position

**Recommendation:** In `MovementWebSocketHandler.HandleTileClick`:
```csharp
var start = entityState.NextTargetCell ?? entityState.CurrentCell;

var path = _pathfinder.FindPath(start, target);
if (path != null && path.Count > 0 && path[0] != start)
{
    Console.WriteLine($"Warning: Path doesn't start at current position");
}
```

### 5. **Connection Management**
**Issue:** No tracking of active connections or their player IDs

**Recommendation:**
```csharp
public class WebSocketServer
{
    private readonly ConcurrentDictionary<string, WebSocket> _connections = new();
    private readonly Dictionary<WebSocket, string> _wsToPlayerId = new();
    
    // Track connections
    // Remove on disconnect
}
```

### 6. **Error Logging**
**Issue:** Minimal logging - errors may go unnoticed

**Recommendation:** Add structured logging:
```csharp
Console.WriteLine($"[ERROR] WebSocket handler: {ex.Message}");
Console.WriteLine($"[INFO] Player {playerId} disconnected");
```

### 7. **Performance Considerations**

#### Tick Rate
**Current:** Fixed 20 ticks/sec  
**Issue:** No framerate compensation for busy system

**Recommendation:** Consider dynamic tick rate or buffer overflow protection

#### Pathfinding
**Issue:** A* pathfinding is synchronous and could block

**Recommendation:**
- Consider async pathfinding for long paths
- Add pathfinding result caching
- Limit pathfinding to certain update intervals

### 8. **Security Issues**

#### No Rate Limiting
**Issue:** Clients can spam move requests

**Recommendation:**
```csharp
private readonly Dictionary<string, DateTime> _lastMoveTime = new();

public bool CanMove(string playerId)
{
    if (!_lastMoveTime.ContainsKey(playerId)) return true;
    return (DateTime.Now - _lastMoveTime[playerId]).TotalSeconds > 0.1;
}
```

#### No Authentication
**Issue:** Any client can connect and register

**Recommendation:**
- Add authentication tokens
- Verify player identity before allowing actions

### 9. **State Snapshot**
**Issue:** `GetSnapshot` only returns current cell, not interpolated position

**Current:**
```csharp
x = s.CurrentCell.X,
y = s.CurrentCell.Y,
```

**Recommendation:** Send interpolated position to Unity client:
```csharp
if (s.NextTargetCell.HasValue)
{
    var progress = s.ProgressToNext;
    x = (float)(s.CurrentCell.X * (1 - progress) + s.NextTargetCell.Value.X * progress);
    y = (float)(s.CurrentCell.Y * (1 - progress) + s.NextTargetCell.Value.Y * progress);
}
else
{
    x = s.CurrentCell.X;
    y = s.CurrentCell.Y;
}
```

### 10. **Memory Leaks**
**Issue:** Player registration only adds to hashset, never removes

**Recommendation:** Track players per connection:
```csharp
public bool UnregisterPlayer(string id)
{
    return _players.Remove(id);
}
```

---

## 📊 Architecture Improvements

### Message Routing
**Current:** Single message handler service with separate dictionaries  
**Better:** Polymorphic message handlers

```csharp
public interface IMessageHandler
{
    Task HandleAsync(WebSocket ws, string message, string currentPlayerId);
}

public class TileClickHandler : IMessageHandler { }
public class StateRequestHandler : IMessageHandler { }
```

### Dependency Injection
**Current:** Manual construction in Program.cs  
**Better:** Use proper DI container

```csharp
var services = new ServiceCollection();
services.AddSingleton<MovementService>();
services.AddSingleton<PlayerService>();
// ... register all services
var provider = services.BuildServiceProvider();
```

### Event System
**Recommendation:** Add event system for player actions:
```csharp
public event Action<string, TilePosition> OnPlayerMoved;
public event Action<string> OnPlayerDisconnected;
```

---

## 📝 Code Quality Improvements

### 1. **Consistent Naming**
- Some methods use async (e.g., `HandleTileClick`)
- Others are synchronous
- Consider async/await consistency

### 2. **Magic Numbers**
**Current:**
```csharp
var delay = TimeSpan.FromMilliseconds(1000.0 / ticksPerSecond);
```

**Better:**
```csharp
const double MILLISECONDS_PER_SECOND = 1000.0;
var delay = TimeSpan.FromMilliseconds(MILLISECONDS_PER_SECOND / ticksPerSecond);
```

### 3. **Validation**
Add input validation:
```csharp
if (path == null || path.Count == 0)
{
    // Log error
    return;
}
```

### 4. **Documentation**
Add XML comments to public APIs:
```csharp
/// <summary>
/// Registers a new entity in the movement system
/// </summary>
/// <param name="playerId">Unique identifier for the player</param>
/// <param name="spawnCell">Initial spawn position</param>
/// <param name="speed">Movement speed in tiles per second</param>
public void RegisterEntity(string playerId, TilePosition spawnCell, float speed = 6f)
```

---

## 🎯 Priority Recommendations

### High Priority
1. ✅ **Fix duplicate tick loops** - DONE
2. ✅ **Add WebSocket error handling** - DONE
3. ⚠️ **Implement player cleanup on disconnect**
4. ⚠️ **Add configuration for spawn positions**
5. ⚠️ **Add rate limiting for move requests**

### Medium Priority
6. Add proper logging system
7. Implement connection tracking
8. Add state validation
9. Improve error messages to clients

### Low Priority
10. Consider async pathfinding
11. Add event system
12. Refactor to dependency injection
13. Add unit tests

---

## 🧪 Testing Recommendations

### Unit Tests Needed
- `MovementService.Tick()` with various delta times
- `RequestMove()` with null and empty paths
- Entity state transitions (Idle → Moving → Idle)
- Pathfinding edge cases

### Integration Tests Needed
- WebSocket connection/disconnection
- Message routing
- Player registration and cleanup
- State snapshot generation

---

## 📚 Additional Notes

### Performance Metrics to Monitor
- Tick duration
- WebSocket message rate
- Memory usage (player count)
- Pathfinding execution time

### Scalability Concerns
- Current implementation is single-server only
- No support for multiple game instances
- No database persistence

### Future Enhancements
- Add support for multiple maps/regions
- Implement spatial partitioning for entity queries
- Add movement prediction/compensation
- Support for different entity types with different movement rules

---

## Summary

**Overall Grade: B+**

The architecture is solid with good separation of concerns. The critical issues (duplicate ticks, error handling, thread safety) have been fixed. Remaining improvements focus on production-readiness, scalability, and robustness.

**Key Takeaways:**
1. Solid foundation for a game server
2. Need better error handling and logging
3. Missing cleanup mechanisms
4. Could benefit from configuration management
5. Consider security and rate limiting



