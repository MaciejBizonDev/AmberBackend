using System.Collections.Generic;

namespace AmberBackend.AI.Composites
{
    /// <summary>
    /// Executes children in order until one succeeds.
    /// Returns Failure if all fail.
    /// </summary>
    public class Selector : BehaviorNode
    {
        private readonly List<BehaviorNode> _children;
        private int _currentIndex = 0;

        public Selector(params BehaviorNode[] children)
        {
            _children = new List<BehaviorNode>(children);
        }

        public override NodeStatus Execute(AIContext context)
        {
            while (_currentIndex < _children.Count)
            {
                var status = _children[_currentIndex].Execute(context);

                if (status == NodeStatus.Success)
                {
                    Reset();
                    return NodeStatus.Success;
                }

                if (status == NodeStatus.Running)
                {
                    return NodeStatus.Running;
                }

                // Failure - try next child
                _currentIndex++;
            }

            // All children failed
            Reset();
            return NodeStatus.Failure;
        }

        public override void Reset()
        {
            _currentIndex = 0;
            foreach (var child in _children)
            {
                child.Reset();
            }
        }
    }
}