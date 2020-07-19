using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace FaceDetectAndRecognize.Core
{
    public class MemoryMessageBus
    {
        static MemoryMessageBus _instance;
        public static MemoryMessageBus Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = new MemoryMessageBus();
                return _instance;
            }
        }

        ConcurrentDictionary<string, ConcurrentQueue<object>> _queue = new ConcurrentDictionary<string, ConcurrentQueue<object>>();

        ConcurrentDictionary<string, ConcurrentStack<object>> _stack = new ConcurrentDictionary<string, ConcurrentStack<object>>();

        ConcurrentDictionary<string, object> _cache = new ConcurrentDictionary<string, object>();

        ConcurrentDictionary<string, ConcurrentDictionary<string, Action<object>>> _channelSubscriber
        = new ConcurrentDictionary<string, ConcurrentDictionary<string, Action<object>>>();
        ConcurrentDictionary<string, bool> _channelTypeIsQueue = new ConcurrentDictionary<string, bool>();
        ConcurrentDictionary<string, DateTime?> _keyExpire = new ConcurrentDictionary<string, DateTime?>();
        ConcurrentDictionary<string, KeyValuePair<DateTime, TimeSpan?>> _keySlideExpire = new ConcurrentDictionary<string, KeyValuePair<DateTime, TimeSpan?>>();

        ConcurrentDictionary<string, ConcurrentDictionary<string, object>> _hashSet = new ConcurrentDictionary<string, ConcurrentDictionary<string, object>>();

        Thread _channelThread;
        private MemoryMessageBus()
        {
            _channelThread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {

                        ThreadPool.QueueUserWorkItem((o) =>
                        {
                            List<KeyValuePair<string, ConcurrentDictionary<string, Action<object>>>> tempChannel;
                            lock (_channelSubscriber)
                            {
                                tempChannel = _channelSubscriber.ToList();
                            }
                            foreach (var i in tempChannel)
                            {
                                var itm = i;
                                ThreadPool.QueueUserWorkItem((o) =>
                                {
                                    if (_channelTypeIsQueue.ContainsKey(itm.Key) && itm.Value.Count > 0)
                                    {
                                        var channelType = _channelTypeIsQueue[itm.Key];
                                        object data = channelType ? Dequeue(itm.Key) : Pop(itm.Key);
                                        if (data != null)
                                        {
                                            List<KeyValuePair<string, Action<object>>> tempSubscribes;
                                            lock (itm.Value)
                                            {
                                                tempSubscribes = itm.Value.ToList();
                                            }

                                            foreach (var subscriber in tempSubscribes)
                                            {
                                                var tempSubsc = subscriber;
                                                ThreadPool.QueueUserWorkItem((o) =>
                                                {
                                                    tempSubsc.Value(data);
                                                });
                                            }
                                        }
                                    }
                                });
                            }

                        });

                        ThreadPool.QueueUserWorkItem((o) =>
                        {
                            List<KeyValuePair<string, DateTime?>> tempExpired;

                            lock (_keyExpire)
                            {
                                tempExpired = _keyExpire.ToList();
                            }

                            var listExpired = tempExpired.Where(i => i.Value != null && i.Value.Value < DateTime.Now).ToList();

                            foreach (var item in listExpired)
                            {
                                var tempItm = item;
                                ThreadPool.QueueUserWorkItem((o) =>
                                {
                                    Clear(tempItm.Key);
                                });
                            }

                        });
                    }
                    catch
                    {
                    }
                    finally
                    {
                        Thread.Sleep(1);
                    }
                }

            });

            _channelThread.Start();
        }

        public void CacheSet<T>(string key, T data, DateTime? expireAt = null)
        {
            _cache[key] = data;
            SetExpire(key, expireAt);
        }

        public void CacheSetUseSlideExpire<T>(string key, T data, TimeSpan? slideInterval = null)
        {
            _cache[key] = data;
            SetExpireUseSlide(key, slideInterval);
        }

        public T CacheGet<T>(string key)
        {
            if (_keySlideExpire.ContainsKey(key))
            {
                var slideInterval = _keySlideExpire[key].Value;
                SetExpireUseSlide(key, slideInterval);
            }
            return _cache.TryGetValue(key, out object val) && val != null ? (T)val : default(T);
        }

        public void HashSet<T>(string key, string field, T data, DateTime? expireAt = null)
        {
            if (!_hashSet.TryGetValue(key, out ConcurrentDictionary<string, object> fields))
            {
                _hashSet[key] = new ConcurrentDictionary<string, object>();
            }
            _hashSet[key][field] = data;

            SetExpire(key, expireAt);
        }
        public void HashSetUseSlideExpire<T>(string key, string field, T data, TimeSpan? slideInterval = null)
        {
            if (!_hashSet.TryGetValue(key, out ConcurrentDictionary<string, object> fields))
            {
                _hashSet[key] = new ConcurrentDictionary<string, object>();
            }
            _hashSet[key][field] = data;
            SetExpireUseSlide(key, slideInterval);
        }
        public T HashGet<T>(string key, string field)
        {
            if (_keySlideExpire.ContainsKey(key))
            {
                var slideInterval = _keySlideExpire[key].Value;
                SetExpireUseSlide(key, slideInterval);
            }

            _hashSet.TryGetValue(key, out ConcurrentDictionary<string, object> filedData);

            return filedData.TryGetValue(field, out object data) && data != null ? (T)data : default(T);
        }

        public ConcurrentDictionary<string, object> HashGetAll(string key)
        {
            _hashSet.TryGetValue(key, out ConcurrentDictionary<string, object> val);
            return val;
        }

        public void SetExpire(string key, DateTime? expireAt = null)
        {
            if (expireAt == null)
            {
                _keyExpire.TryRemove(key, out expireAt);

                _keySlideExpire.TryRemove(key, out KeyValuePair<DateTime, TimeSpan?> oldSlideExpired);
            }
            else
            {
                _keyExpire[key] = expireAt;
            }
        }

        public void SetExpireUseSlide(string key, TimeSpan? slideInterval = null)
        {
            DateTime? expireAt = null;
            if (slideInterval != null)
            {
                expireAt = DateTime.Now.Add(slideInterval.Value);

                _keySlideExpire[key] = new KeyValuePair<DateTime, TimeSpan?>(expireAt.Value, slideInterval);
            }

            SetExpire(key, expireAt);
        }

        public void Enqueue<T>(string queueName, T data, DateTime? expireAt = null)
        {
            if (!_queue.TryGetValue(queueName, out ConcurrentQueue<object> queueData))
            {
                queueData = new ConcurrentQueue<object>();
                _queue[queueName] = queueData;
            }

            queueData.Enqueue(data);

            SetExpire(queueName, expireAt);
        }

        object Dequeue(string queueName)
        {
            if (_queue.TryGetValue(queueName, out ConcurrentQueue<object> queueData) && queueData != null)
            {
                if (queueData.TryDequeue(out object data) && data != null)
                {
                    return data;
                }
            }

            return null;
        }

        public T Dequeue<T>(string queueName)
        {
            var data = Dequeue(queueName);
            return data == null ? default(T) : (T)data;
        }

        public void Push<T>(string stackName, T data, DateTime? expireAt = null)
        {
            if (!_stack.TryGetValue(stackName, out ConcurrentStack<object> stackData))
            {
                stackData = new ConcurrentStack<object>();
                _stack[stackName] = stackData;
            }

            stackData.Push(data);

            SetExpire(stackName, expireAt);
        }
        object Pop(string stackName)
        {
            if (_stack.TryGetValue(stackName, out ConcurrentStack<object> stackData) && stackData != null)
            {
                if (stackData.TryPop(out object data) && data != null)
                {
                    return data;
                }
            }

            return null;
        }
        public T Pop<T>(string stackName)
        {
            var data = Pop(stackName);
            return data == null ? default(T) : (T)data;
        }

        string ScopedChannelName(string channelName)
        {
            return $"Channel:{channelName}";
        }

        public void Publish<T>(string channelName, T data, DateTime? expireAt = null)
        {
            channelName = ScopedChannelName(channelName);
            _channelTypeIsQueue[channelName] = true;
            Enqueue(channelName, data, expireAt);
        }

        public void PublishUseStack<T>(string channelName, T data, DateTime? expireAt = null)
        {
            channelName = ScopedChannelName(channelName);
            _channelTypeIsQueue[channelName] = false;
            Push(channelName, data, expireAt);
        }

        public void Subscribe<T>(string channelName, string subscribeName, Action<T> onMessage)
        {
            channelName = ScopedChannelName(channelName);
            if (!_channelSubscriber.TryGetValue(channelName, out ConcurrentDictionary<string, Action<object>> subscribers))
            {
                subscribers = new ConcurrentDictionary<string, Action<object>>();
                _channelSubscriber[channelName] = subscribers;
            }

            subscribers[subscribeName] = (o) =>
            {
                if (o == null) onMessage(default(T));
                else onMessage((T)o);
            };
        }

        public void Unsubscribe(string channelName, string subscribeName)
        {
            channelName = ScopedChannelName(channelName);
            if (!_channelSubscriber.TryGetValue(channelName, out ConcurrentDictionary<string, Action<object>> subscribers))
            {
                subscribers = new ConcurrentDictionary<string, Action<object>>();
            }

            subscribers.TryRemove(subscribeName, out Action<object> oldVal);
        }

        public int CountQueue(string queueName)
        {
            if (_queue.ContainsKey(queueName) == false) return 0;
            return _queue[queueName].Count;
        }

        public int CountStack(string stackName)
        {
            if (_stack.ContainsKey(stackName) == false) return 0;
            return _queue[stackName].Count;
        }

        public int CountDataInChannel(string channelName)
        {
            channelName = ScopedChannelName(channelName);
            return CountQueue(channelName);
        }

        public int CountDataInChannelUseStack(string channelName)
        {
            channelName = ScopedChannelName(channelName);
            return CountStack(channelName);
        }

        public int CountSubscribeInChannel(string channelName)
        {
            if (_channelSubscriber.ContainsKey(channelName) == false) return 0;
            return _queue[channelName].Count;
        }

        public List<string> ListSubscriberInChannel(string channelName)
        {
            if (_channelSubscriber.ContainsKey(channelName) == false) return new List<string>();

            return _channelSubscriber[channelName].Keys.ToList();
        }

        public void Clear(string key)
        {
            _cache.TryRemove(key, out object cacheVal);

            _queue.TryRemove(key, out ConcurrentQueue<object> queueVal);

            _stack.TryRemove(key, out ConcurrentStack<object> stackVal);

            var channelName = key;
            _channelSubscriber.TryRemove(channelName, out ConcurrentDictionary<string, Action<object>> oldChannelVal);
            _channelTypeIsQueue.TryRemove(channelName, out bool oldTypeVal);

            _keyExpire.TryRemove(key, out DateTime? oldExpired);
            _keySlideExpire.TryRemove(key, out KeyValuePair<DateTime, TimeSpan?> oldSlideExpired);

            _hashSet.TryRemove(key, out ConcurrentDictionary<string, object> oldHashset);
        }

        public List<string> ListAllKey()
        {
            List<string> keys = new List<string>();

            lock (_cache)
            {
                keys.AddRange(_cache.Select(i => i.Key).ToList());
            }
            lock (_queue)
            {
                keys.AddRange(_cache.Select(i => i.Key).ToList());
            }
            lock (_stack)
            {
                keys.AddRange(_cache.Select(i => i.Key).ToList());
            }
            lock (_channelSubscriber)
            {
                keys.AddRange(_cache.Select(i => i.Key).ToList());
            }
            lock (_channelTypeIsQueue)
            {
                keys.AddRange(_cache.Select(i => i.Key).ToList());
            }
            lock (_keyExpire)
            {
                keys.AddRange(_cache.Select(i => i.Key).ToList());
            }
            lock (_keySlideExpire)
            {
                keys.AddRange(_cache.Select(i => i.Key).ToList());
            }
            lock (_hashSet)
            {
                keys.AddRange(_hashSet.Select(i => i.Key).ToList());
            }
            keys = keys.Distinct().ToList();

            return keys;
        }

        public void ClearAll()
        {
            var keys = ListAllKey();
            ThreadPool.QueueUserWorkItem((o) =>
            {
                foreach (var k in keys)
                {
                    Clear(k);
                }
            });
        }
    }

}
