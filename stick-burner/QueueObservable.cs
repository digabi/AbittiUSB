using System;
using System.Reactive.Linq;
using System.Threading.Tasks.Dataflow;

namespace Ytl.AbittiUsb
{
    public class QueueObservable<T>
    {
        private readonly BufferBlock<T> queue = new BufferBlock<T>();

        public async void Notify(T t)
        {
            await queue.SendAsync(t);
        }

        public IObservable<T> GetObservable()
        {
            return Observable.Create<T>(observer =>
            {
                WireQueueReceivingObserver(observer);
                return () => { queue.Complete(); };
            });
        }

        private async void WireQueueReceivingObserver(IObserver<T> observer)
        {
            while (await queue.OutputAvailableAsync())
            {
                observer.OnNext(await queue.ReceiveAsync());
            }
        }
    }
}