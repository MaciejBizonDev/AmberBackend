using System;

namespace AmberBackend.AI
{
    /// <summary>
    /// Result of executing a behavior node.
    /// </summary>
    public enum NodeStatus
    {
        Success,
        Failure,
        Running
    }

    /// <summary>
    /// Base class for all behavior tree nodes.
    /// </summary>
    public abstract class BehaviorNode
    {
        /// <summary>
        /// Execute this node. Returns status.
        /// </summary>
        public abstract NodeStatus Execute(AIContext context);

        /// <summary>
        /// Reset node state (called when tree restarts).
        /// </summary>
        public virtual void Reset() { }
    }
}