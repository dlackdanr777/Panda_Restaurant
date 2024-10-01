namespace Muks.Tween
{
    public class TweenWait : TweenData
    {
        public override void Clear()
        {
            base.Clear();
            Tween.DequeueTweenWait(this);
        }


        protected override void SetData(TweenDataSequence dataSequence)
        {
            base.SetData(dataSequence);
        }


        protected override void Update()
        {
            base.Update();
        }


        protected override void TweenCompleted()
        {
        }
    }
}

