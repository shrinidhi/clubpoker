// 📁 CREATE AT:  Assets/Scripts/Utils/UnityThread.cs
// 📋 ACTION:     New file — right-click Utils/ → Create → C# Script → UnityThread
// 🔗 NAMESPACE:  ClubPoker.Networking
// ⚙️ ASMDEF:     ClubPoker.Networking
//
// Dispatches actions from background threads (e.g. SocketIOUnity callbacks)
// back to the Unity main thread so Unity APIs can be called safely.
//
// Setup: add UnityThread as a GameObject in the Bootstrap scene.
// Usage: UnityThread.ExecuteInUpdate(() => { /* your Unity code */ });

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClubPoker.Networking
{
    public class UnityThread : MonoBehaviour
    {
        private static readonly Queue<Action> _queue   = new Queue<Action>();
        private static readonly object        _lock    = new object();

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Queue an action to be executed on the Unity main thread during the next Update().
        /// Safe to call from any thread.
        /// </summary>
        public static void ExecuteInUpdate(Action action)
        {
            if (action == null) return;
            lock (_lock)
            {
                _queue.Enqueue(action);
            }
        }

        private void Update()
        {
            lock (_lock)
            {
                while (_queue.Count > 0)
                    _queue.Dequeue()?.Invoke();
            }
        }
    }
}