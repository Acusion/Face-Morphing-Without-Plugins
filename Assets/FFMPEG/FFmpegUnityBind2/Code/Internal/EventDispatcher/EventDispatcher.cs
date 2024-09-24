using System.Collections.Generic;
using UnityEngine;

namespace FFmpegUnityBind2.Internal
{
    class EventDispatcher : MonoBehaviour
    {
        static readonly Queue<Event> events = new Queue<Event>();

        public static void Invoke(Event @event)
        {
            if (@event.type == EventType.OnLog)
            {
                @event = null;
                return;
            }
            events.Enqueue(@event);
        }

        static void TryExecute()
        {
            if (events.Count > 0)
            {
                var ev = events.Dequeue();
                ev?.Handle();
                ev = null;
            }
        }

        void Update()
        {
            TryExecute();
        }

        void OnDestroy()
        {
            TryExecute();
        }

        public static void ClearEvents()
        {
            events.Clear();
            
        }

        [ContextMenu("Test")]
        void Test()
        {
            Debug.Log(events.Count);
        }
    }
}
