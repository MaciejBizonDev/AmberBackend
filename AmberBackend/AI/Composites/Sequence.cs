using System.Collections.Generic;

namespace AmberBackend.AI.Composites
{
    /// <summary>
    /// Executes children in order until one fails.
    /// Returns Success if all succeed.
    /// </summary>
    public class Sequence : BehaviorNode
    {
        private readonly List<BehaviorNode> _children;
        private int _currentIndex = 0;

        public Sequence(params BehaviorNode[] children)
        {
            _children = new List<BehaviorNode>(children);
        }

        public override NodeStatus Execute(AIContext context)
        {
            while (_currentIndex < _children.Count)
            {
                var status = _children[_currentIndex].Execute(context);

                if (status == NodeStatus.Failure)
                {
                    Reset();
                    return NodeStatus.Failure;
                }

                if (status == NodeStatus.Running)
                {
                    return NodeStatus.Running;
                }

                // Success - move to next child
                _currentIndex++;
            }

            // All children succeeded
            Reset();
            return NodeStatus.Success;
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