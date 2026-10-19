using System;

namespace ExtractionShooter.AI
{
    public enum NodeStatus { Failure, Success, Running }

    // 노드 객체와 델리게이트는 트리를 구성할 때만 만들고 판단 중에는 재사용한다.
    public abstract class BehaviourNode
    {
        public abstract NodeStatus Tick();
        public virtual void Abort() { }
    }

    public class ReactiveSelector : BehaviourNode
    {
        private readonly EligibleAction[] children;
        private int active = -1;

        public ReactiveSelector(params EligibleAction[] children)
        {
            this.children = children ?? throw new ArgumentNullException(nameof(children));
        }

        public override NodeStatus Tick()
        {
            for (int i = 0; i < children.Length; i++)
            {
                // 이전 행동을 중단한 뒤 새 행동을 실행해야 Stop이 새 이동을 취소하지 않는다.
                // 조건만 검사하고 선택된 행동만 실행한다.
                if (!children[i].IsEligible)
                    continue;
                if (active != i && active >= 0) 
                { 
                    children[active].Abort(); 
                    active = -1; 
                }
                NodeStatus status = children[i].Tick();
                if (status == NodeStatus.Failure) continue;
                active = status == NodeStatus.Running ? i : -1;
                return status;
            }
            if (active >= 0) children[active].Abort();
            active = -1;
            return NodeStatus.Failure;
        }

        public override void Abort()
        {
            if (active >= 0) children[active].Abort();
            active = -1;
        }
    }

    // 선택 전에 조건만 읽어 이전 행동의 종료와 새 행동의 시작 순서를 보장한다.
    public class EligibleAction : BehaviourNode
    {
        private readonly Func<bool> condition;
        private readonly Func<NodeStatus> execute;
        private readonly Action stop;
        private bool running;
        public bool IsEligible => condition();

        public EligibleAction(Func<bool> condition, Func<NodeStatus> execute, Action stop)
        {
            this.condition = condition;
            this.execute = execute;
            this.stop = stop;
        }
        public override NodeStatus Tick()
        {
            NodeStatus status = execute();
            // 실행 중이던 행동이 실패하거나 끝나면 남아 있는 이동 등의 부수 효과를 정리한다.
            if (running && status != NodeStatus.Running) stop?.Invoke();
            running = status == NodeStatus.Running;
            return status;
        }
        public override void Abort()
        {
            if (!running) return;
            running = false;
            stop?.Invoke();
        }
    }

    // 행동 중에서도 순차적으로 실행해야 하는 행동을 묶어 관리한다.
    public class Sequence : BehaviourNode
    {
        private readonly BehaviourNode[] children;
        private int childIndex = 0;

        public Sequence(params BehaviourNode[] children)
        {
            this.children = children ?? throw new ArgumentNullException(nameof(children));
        }

        // 수행해야할 행동들을 결과에 따라 순서대로 실행한다.
        public override NodeStatus Tick()
        {
            for (; childIndex < children.Length; childIndex++)
            {
                NodeStatus status = children[childIndex].Tick();
                if (status == NodeStatus.Running)
                    return NodeStatus.Running;
                if (status == NodeStatus.Failure)
                {
                    childIndex = 0;
                    return NodeStatus.Failure;
                }
            }

            // 모든 행동이 성공하면 Sequence를 성공 처리하고 다음 행동을 위해 인덱스를 초기화한다.
            childIndex = 0;
            return NodeStatus.Success;
        }

        // 실행중인 시퀀스를 중단하고 진행 위치를 초기화한다.
        public override void Abort()
        {
            if (childIndex < children.Length)
                children[childIndex].Abort();
            childIndex = 0;
        }
    }
}
