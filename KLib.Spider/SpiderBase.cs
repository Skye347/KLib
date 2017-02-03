using System;
using System.Collections.Generic;
using KLib.HTTP;
using System.Collections.Concurrent;
using System.Threading;
using KLib.Spider.Middleware;
using System.Linq;

namespace KLib.Spider
{
    abstract public class SpiderBase
    {
        public List<string> startList;
        abstract public SpiderRequest Start(SpiderResponse response);
        public void AddRequest(SpiderRequest request)
        {
            Spider.AddRequest(request);
        }
        internal List<RequestMiddlewareBase> _RequestMiddleware = new List<RequestMiddlewareBase>
        {
            //new ExampleRequestMiddleware()
        };
        internal List<ResponseMiddlewareBase> _ResponseMiddleware = new List<ResponseMiddlewareBase>
        {
            new RedirectMiddleware()
        };
        public List<RequestMiddlewareBase> RequestMiddleware
        {
            get
            {
                return _RequestMiddleware;
            }
            set
            {
                _RequestMiddleware = _RequestMiddleware.Union(value).ToList();
            }
        }
        public List<ResponseMiddlewareBase> ResponseMiddleware
        {
            get
            {
                return _ResponseMiddleware;
            }
            set
            {
                _ResponseMiddleware = _ResponseMiddleware.Union(value).ToList();
            }
        }
    }

    public class SpiderResponse
    {
        public SpiderResponse(SpiderRequest request, HTTPResponse HttpResponse)
        {
            this.request = request;
            this.httpResponse = HttpResponse;
        }
        public SpiderRequest request { get; private set; }
        public HTTPResponse httpResponse { get; private set; }
        public static SpiderResponse Make(SpiderRequest request, HTTPResponse HttpResponse)
        {
            return new SpiderResponse(request, HttpResponse);
        }
    }

    public class SpiderRequest
    {
        public HTTPMethod method;
        public string Url;
        public Spider.SpiderRequestCallback callback;
        public HTTPCookie Cookie;
        public string PostData;
        public Dictionary<string, string> AdditionHeader;
        public static SpiderRequest Make(String Url,Spider.SpiderRequestCallback callback)
        {
            SpiderRequest newRequest = new SpiderRequest();
            newRequest.Url = Url;
            newRequest.callback = callback;
            return newRequest;
        }
        public SpiderRequest AddHeader(Dictionary<string, string> Header)
        {
            this.AdditionHeader = Header;
            return this;
        }
        public SpiderRequest AddCookie(HTTPCookie Cookie)
        {
            this.Cookie.AddCookie(Cookie);
            return this;
        }
        public SpiderRequest AddPostData(string PostData)
        {
            this.PostData = PostData;
            method = HTTPMethod.POST;
            return this;
        }
    }

    public static class Spider
    {
        public delegate SpiderRequest SpiderRequestCallback(SpiderResponse response);
        public static SpiderBase currentSpider { get; private set; }
        public static bool isRunning { get; private set; }
        public static SpiderRequest MakeRequest(string Url, SpiderRequestCallback callback)
        {
            return SpiderRequest.Make(Url, callback);
        }
        public static bool loadSpider(SpiderBase spider)
        {
            if (isRunning)
            {
                return false;
            }
            currentSpider = spider;
            foreach(var item in currentSpider.startList)
            {
                requestList.Enqueue(MakeRequest(item, currentSpider.Start));
            }
            return true;
        }
        static SpiderRequest RunRequestMiddleware(List<RequestMiddlewareBase> middlewareList,SpiderRequest request)
        {
            foreach(var item in middlewareList)
            {
                request = item.Process(request);
            }
            return request;
        }
        static SpiderResponse RunResponseMiddleware(List<ResponseMiddlewareBase> middlewareList,SpiderResponse response)
        {
            foreach(var item in middlewareList)
            {
                response = item.Process(response);
                if (response == null)
                {
                    return null;
                }
            }
            return response;
        }
        private static ConcurrentQueue<SpiderRequest> requestList = new ConcurrentQueue<SpiderRequest>();
        private static object waitLock = new object();
        internal static void AddRequest(SpiderRequest request)
        {
            if (!isRunning)
            {
                return;
            }
            requestList.Enqueue(request);
            lock (waitLock)
            {
                Monitor.Pulse(waitLock);
            }
        }
        private static SpiderRequest GetNext()
        {
            SpiderRequest next = null;
            while (isRunning)
            {
                requestList.TryDequeue(out next);
                if (next != null)
                {
                    next = RunRequestMiddleware(currentSpider._RequestMiddleware, next);
                    break;
                }
                else
                {
                    if (isRunning == false)
                    {
                        next = null;
                        break;
                    }
                    lock (waitLock)
                    {
                        Monitor.Wait(waitLock);
                    }
                }
            }
            return next;
        }
        public static void Stop()
        {
            isRunning = false;
            lock (waitLock)
            {
                Monitor.Pulse(waitLock);
            }
        }
        public static HTTPRequest HttpCallback(HTTPResponse response)
        {
            SpiderRequest request = response.request.Addition as SpiderRequest;
            var sResponse = new SpiderResponse(request, response);
            sResponse = RunResponseMiddleware(currentSpider._ResponseMiddleware, sResponse);
            if (sResponse == null)
            {
                return null;
            }
            else
            {
                var n = request.callback(sResponse);
                if (!isRunning)
                {
                    return null;
                }
                if (n == null)
                {
                    return null;
                }
                n = RunRequestMiddleware(currentSpider._RequestMiddleware, n);
                return response.MakeRequest(n.method, n.Url, n, HttpCallback, n.Cookie, n.AdditionHeader, n.PostData);
            }
        }
        public static void Run()
        {
            HTTPOp.Init();
            isRunning = true;
            SpiderRequest next;
            while (isRunning)
            {
                next = GetNext();
                if (next == null)
                {
                    break;
                }
                if (isRunning)
                {
                    HTTPError err=HTTPOp.Request(next.method, next.Url, next, HttpCallback);
                    if (err != HTTPError.SUCCESS)
                    {
                        Log.Log.log("DNS Error:"+ next.Url, Log.Log.ERROR, "Spider.Run");
                    }
                }
            }
            HTTPOp.StopCore();
        }
    }
}
