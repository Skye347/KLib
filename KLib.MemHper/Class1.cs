using System;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;

namespace KLib.MemHper
{
    public abstract class Buffer
    {
        public delegate void SetBufferMethod(byte[] buf, int a, int b);
        public abstract bool SetBuffer(SetBufferMethod method,int Size);
    }

    public class SimpleBuffer : Buffer
    {
        public override bool SetBuffer(SetBufferMethod method, int Size)
        {
            method(new byte[Size], 0, Size);
            return true;
        }
    }

    public abstract class Cache
    {
        public abstract object Get(object Key);
        public abstract bool Set(object Key, object Value);
    }

    public class MicrosoftMemCache : Cache
    {
        private MemoryCache cache;
        public MicrosoftMemCache()
        {
            var options = new MemoryCacheOptions();
            cache = new MemoryCache(options);
        }
        public override object Get(object Key)
        {
            return cache.Get(Key);
        }
        public override bool Set(object Key, object Value)
        {
            cache.Set<object>(Key, Value);
            return true;
        }
        public bool Set<ItemType>(object Key, ItemType Value)
        {
            cache.Set<ItemType>(Key, Value);
            return true;
        }
    }
}
