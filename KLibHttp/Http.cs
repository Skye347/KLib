using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Net.Http;
using KLib.NetCore.Callback;
using System.Text;
using KLib.NetCore;
using static KLib.Log.Log;
using KLib.HTTP.ParseHelper;
using System.Threading;
using System.Runtime.CompilerServices;

namespace KLib.HTTP
{
    using HTTPHeader = Dictionary<string, string>;

    public enum HTTPError
    {
        DNSERROR,
        SUCCESS,
        UNSUPPORT,
        TIMEOUT
    }

    public class HTTPCookie
    {
        private Dictionary<string, string> _Cookie=new Dictionary<string,string>();
        public string _Domain="";
        public HTTPCookie AddCookie(HTTPCookie cookie)
        {
            if (cookie == null)
            {
                return this;
            }
            return AddCookie(cookie._Cookie, cookie._Domain);
        }
        public bool ValidDomain(string domain)
        {
            if (this._Domain == domain)
            {
                return true;
            }
            return false;
        }
        public HTTPCookie AddCookie(string Key,string Value,string domain)
        {
            if (ValidDomain(domain))
            {
                if (_Cookie.ContainsKey(Key))
                {
                    _Cookie[Key] = Value;
                }
                else
                {
                    _Cookie.Add(Key, Value);
                }
            }
            return null;
        }
        public HTTPCookie AddCookie(Dictionary<string,string> cookie,string domain)
        {
            if (ValidDomain(domain))
            {
                foreach(var item in cookie)
                {
                    if (_Cookie.ContainsKey(item.Key))
                    {
                        _Cookie[item.Key] = item.Value;
                    }
                    else
                    {
                        _Cookie.Add(item.Key, item.Value);
                    }
                }
                return this;
            }
            else
            {
                HTTPCookie newCookie = new HTTPCookie();
                newCookie._Cookie = cookie;
                return newCookie;
            }
        }
        public Dictionary<string,string> GetCookie(string domain)
        {
            if (domain.Contains(_Domain))
            {
                return _Cookie;
            }
            else
            {
                return null;
            }
        }
    }

    public class HTTPOp : CallbackCollection
    {
        public static HTTPRequest ConnectedRequest;
        public static int Id = 0;
        void useHTTP()
        {
            UseTcp();
        }
        KLib.NetCore.Protocol.ProtocolOpSsl useHTTPS()
        {
            var ssl=new KLib.NetCore.Protocol.ProtocolOpSsl();
            UseCustom(ssl);
            return ssl;
        }
        private static KLib.NetCore.Protocol.ProtocolOpSsl sslProtocol;
        private static object newConnectLock = new object();
        new private static Core currentCore;
        private static Core httpsCore;
        public static void Init()
        {
            HTTPOp protocolInstance = new HTTPOp();
            protocolInstance.useHTTP();
            Core newCore = new UniAsynCore();
            newCore.SetClient(protocolInstance, false);
            protocolInstance.BindCore(newCore);
            currentCore = newCore;
            HTTPOp httpsProtocol = new HTTPOp();
            sslProtocol=httpsProtocol.useHTTPS();
            httpsCore = new UniAsynCore();
            httpsCore.SetClient(httpsProtocol, false);
            httpsProtocol.BindCore(httpsCore);
        }
        public static HTTPError Request(HTTPRequest request)
        {
            IPAddress ipAddress = AddressHelper.HostToIPAddress(request.Host);
            if (ipAddress == null)
            {
                return HTTPError.DNSERROR;
            }
            while (ConnectedRequest != null)
            {
                lock (newConnectLock)
                {
                    Monitor.Wait(newConnectLock);
                }
            }
            if (request.Scheme == "http")
            {
                //log("request http:" + request.Host, INFO, "HTTP.request");
                ConnectedRequest = request;
                currentCore.Connect(ipAddress.ToString(), 80);
            }
            else if(request.Scheme=="https")
            {
                //log("request https:" + request.Host, INFO, "HTTP.request");
                ConnectedRequest = request;
                sslProtocol.SetTargetHost(ipAddress, request.Host);
                httpsCore.Connect(ipAddress.ToString(), 443);
            }
            else
            {
                return HTTPError.UNSUPPORT;
            }
            return HTTPError.SUCCESS;
        }
        public static HTTPError Request(HTTPMethod method,string Url,object Addition,HTTPRequest.RequestCallback callback, HTTPCookie cookie = null, HTTPHeader additionHeader = null,string PostData=null)
        {
            HTTPStateObject state = new HTTPStateObject();
            HTTPRequest request = new HTTPRequest()
            {
                method = method
            };
            Uri path = new Uri(Url);
            request.Cookie = cookie?.GetCookie(path.Host);
            request.Header = additionHeader;
            request.Callback = callback;
            request.Addition = Addition;
            request.SetUrl(path.Scheme,path.PathAndQuery,path.Host);
            request.PostData = PostData;
            return Request(request);
        }
        override public object Received(byte[] data, UniNetObject connection, out NetCore.Error.NetCoreException err, object Addition)
        {
            err = null;
            //log("id:" + (connection.stateObject as HTTPStateObject)?.request.id, INFO, "HTTP.Received");
            if (connection.CompleteTime != 0)
            {
                //log("time:" + connection.CompleteTime, INFO, "HTTP.Received");
            }
            var state = ProcessHTTPResponse(data, Addition as HTTPStateObject,out var nextRequest);
            if (nextRequest != null)
            {
                (state as HTTPStateObject).request = nextRequest;
                //log("send:" + nextRequest.Host, INFO, "HTTP.Received");
                connection.Write(nextRequest.ToByte(), out err);
            }
            else
            {
                if((state as HTTPStateObject).complete == HTTPStateComplete.Closing)
                {
                    connection.Close();
                    err = new NetCore.Error.NetCoreException();
                }
            }
            return state;
        }
        public override void Aborted(UniNetObject connection, object Addition)
        {
            HTTPStateObject state = Addition as HTTPStateObject;
            if (state.request.Done == false)
            {
                HTTPOp.Request(state.request);
            }
        }
        public override void Timeout(UniNetObject connection, object Addition)
        {
            HTTPStateObject state = Addition as HTTPStateObject;
            log("id:" + state?.request.id, ERROR, "HTTP.Received");
            log("timeout:" + connection.CompleteTime, ERROR, "HTTP.Received");
            log("connection info:" + state?.Length, ERROR, "HTTP.Received");
        }
        private static HTTPRequest ProcessResponse(HTTPResponse response,HTTPStateObject state)
        {
            state.request.Done = true;
            response.request = state.request;
            if (response.bodyBytesLength == 0)
            {
                response.bodyBytesLength = Encoding.UTF8.GetByteCount(response.body);
            }
            return state.request.Callback(response);
        }
        override public object Connected(UniNetObject connection, out NetCore.Error.NetCoreException err)
        {
            err = null;
            HTTPRequest request=null;
            if (ConnectedRequest != null)
            {
                request = ConnectedRequest;
                connection.Write(ConnectedRequest.ToByte(), out err);
                ConnectedRequest = null;
            }
            lock (newConnectLock)
            {
                Monitor.Pulse(newConnectLock);
            }
            var state = new HTTPStateObject()
            {
                request = request
            };
            return state;
        }
        
        public object ProcessHTTPResponse(byte[] data, HTTPStateObject state,out HTTPRequest request)
        {
            //string dataAsString = Encoding.UTF8.GetString(data);
            if (state == null)
            {
                throw new InvalidOperationException();
            }
            try
            {
                //var tmpString = Encoding.UTF8.GetString(data);
                if (state.complete==HTTPStateComplete.Init)
                {//process normally
                    HTTPResponse response;
                    if (state.waitedResponse == null)
                    {
                        response = new HTTPResponse();
                    }
                    else
                    {
                        response = state.waitedResponse;
                    }
                    response.Parse(data);
                    if (response.headerDataEnds == -1)
                    {
                        state.waitedResponse = response;
                        state.complete = HTTPStateComplete.incompletedHeader;
                        request = null;
                        return state;
                    }
                    else if (response.header.ContainsKey("Transfer-Encoding") && response.header["Transfer-Encoding"].Contains("chunked"))
                    {
                        state.waitedResponse = response;
                        state.complete = HTTPStateComplete.isChunking;
                        request = null;
                        return state;
                    }
                    else if (response.header.ContainsKey("Content-Length"))
                    {
                        state.Length = int.Parse(response.header["Content-Length"]) - response.bodyBytesLength;
                        if (state.Length == 0)
                        {
                            request = ProcessResponse(response, state);
                            if (request == null)
                            {
                                state.complete = HTTPStateComplete.Closing;
                            }
                            return state;
                        }
                        state.complete = HTTPStateComplete.isReadingByLength;
                        state.waitedResponse = response;
                        request = null;
                        return state;
                    }
                    else
                    {
                        request = ProcessResponse(response, state);
                        if (request == null)
                        {
                            state.complete = HTTPStateComplete.Closing;
                        }
                        return state;
                    }
                }
                else if (state.complete == HTTPStateComplete.isChunking)
                {//process chunked
                    //state.waitedResponse.body += Encoding.UTF8.GetString(data);
                    state.waitedResponse.AddBody(data);
                    int index = data.Length - 1;
                    if (data[index] == 10 && data[index - 1] == 13 && data[index - 2] == 10 && data[index - 3] == 13 && data[index - 4] == 48)
                    {//"0\r\n\r\n"
                        state.waitedResponse.body = state.waitedResponse.body.Substring(6, state.waitedResponse.body.Length - 12);
                        request=ProcessResponse(state.waitedResponse, state);
                        state.waitedResponse = null;
                        state.complete = HTTPStateComplete.Init;
                        if (request == null)
                        {
                            state.complete = HTTPStateComplete.Closing;
                        }
                        return state;
                    }
                    else
                    {
                        request = null;
                        return state;
                    }
                }
                else if (state.complete==HTTPStateComplete.isReadingByLength)
                {//
                    state.Length -= data.Length;
                    state.waitedResponse.AddBody(data);
                    //var waitedData = Encoding.UTF8.GetString(data);
                    //state.Length -= Encoding.UTF8.GetByteCount(waitedData);
                    //state.waitedResponse.body += waitedData;
                    //state.waitedResponse.bodyBytesLength += Encoding.UTF8.GetByteCount(waitedData);
                    if (state.Length <= 0)
                    {
                        request=ProcessResponse(state.waitedResponse, state);
                        state.waitedResponse = null;
                        state.complete = HTTPStateComplete.Init;
                        if (request == null)
                        {
                            state.complete = HTTPStateComplete.Closing;
                        }
                        return state;
                    }
                    else
                    {
                        request = null;
                        return state;
                    }
                }
                else if(state.complete==HTTPStateComplete.incompletedHeader)//incompleted header
                {
                    state.waitedResponse.AddBody(data);
                    state.waitedResponse.Parse(state.waitedResponse.binaryData);
                    if (state.waitedResponse.headerDataEnds == -1)
                    {
                        request = null;
                        return state;
                    }
                    else
                    {
                        state.complete = HTTPStateComplete.Init;
                        return ProcessHTTPResponse(state.waitedResponse.binaryData, state, out request);
                    }
                }
                else
                {
                    request = ProcessResponse(state.waitedResponse, state);
                    if (request == null)
                    {
                        state.complete = HTTPStateComplete.Closing;
                    }
                    return state;
                }
                //if (state.isChunking || state.isReadingByLength)
                //{
                //    return true;
                //}
                //else
                //{
                //    return false;
                //}
            }
            catch (IndexOutOfRangeException e)
            {
                log(e.Message, ERROR, "ProcessHTTPResponse");
                log(data.Length.ToString(), ERROR, "ProcessHTTPResponse");
                log(Encoding.UTF8.GetString(data), ERROR, "ProcessHTTPResponse");
                //log(state.waitedResponse.body, ERROR, "ProcessHTTPResponse");
                throw e;
                //request = null;
                //return state;
            }
        }
    }

    public class HTTPStateObject
    {
        public HTTPStateComplete complete = HTTPStateComplete.Init;
        public HTTPResponse waitedResponse;
        public int Length;
        public HTTPRequest request;
    }

    public enum HTTPStateComplete
    {
        isChunking,
        isReadingByLength,
        incompletedHeader,
        Init,
        Closing
    }

    public class HTTPRequest
    {
        public HTTPRequest()
        {
            Interlocked.Increment(ref HTTPOp.Id);
            id = HTTPOp.Id;
        }
        public delegate HTTPRequest RequestCallback(HTTPResponse response);
        public int id;
        public HTTPMethod method;
        public string Url;
        public string Host;
        public string Scheme;
        public bool Done = false;
        public object Addition;
        public RequestCallback Callback;
        public Dictionary<string, string> Header;
        public Dictionary<string, string> Cookie;
        public string PostData;
        public HTTPRequest ClearHeader()
        {
            this.Header = null;
            return this;
        }
        public void SetUrl(string Url)
        {
            Uri path = new Uri(Url);
            this.Url = path.PathAndQuery;
            this.Host = path.Host;
        }
        public void SetUrl(string Scheme,string Url,string Host)
        {
            this.Url = Url;
            this.Host = Host;
            this.Scheme = Scheme;
        }
        public HTTPRequest CopyTo()
        {
            HTTPRequest request = new HTTPRequest();
            request.Addition = this.Addition;
            request.Callback = this.Callback;
            request.Host = this.Host;
            request.method = this.method;
            request.Scheme = this.Scheme;
            //if (this.Cookie != null)  request.Cookie = new Dictionary<string, string>(this.Cookie);
            //if (this.Header != null)  request.Header = new Dictionary<string, string>(this.Header);
            return request;
        }
        public Byte[] ToByte()
        {
            String requestString = this.GetHttpRequest();
            return Encoding.ASCII.GetBytes(requestString);
        }
        private Dictionary<HTTPMethod, String> HTTPMethodString = new Dictionary<HTTPMethod, String>
        {
            {HTTPMethod.GET,"GET"},
            {HTTPMethod.POST,"POST"}
        };
        public String GetHttpRequest()
        {
            string line1 = "";
            line1 += (
                HTTPMethodString[method] + " "
                + Url + " "
                + "HTTP/1.1"
                );
            string line2 = "";
            line2 += (
                "Host: "
                + Host
            );
            string result = line1 + "\n" + line2 + "\n";
            if (HTTPMethod.POST == method)
            {
                if (Header == null)
                {
                    Header = new HTTPHeader();
                }
                if (Header.ContainsKey("Content-Length"))
                {
                    Header["Content-Length"] = Encoding.UTF8.GetByteCount(PostData).ToString();
                }
                else
                {
                    Header.Add("Content-Length", Encoding.UTF8.GetByteCount(PostData).ToString());
                }
            }
            if (Header != null)
            {
                StringBuilder AdditionHeader = new StringBuilder();
                foreach (var item in Header)
                {
                    if (item.Key == "Host") continue;
                    AdditionHeader.AppendFormat("{0}:{1}\n", item.Key, item.Value);
                }
                result += AdditionHeader.ToString();
            }
            if (this.Cookie != null)
            {
                StringBuilder Cookie = new StringBuilder("Cookie:");
                foreach (var cookie in this.Cookie)
                {
                    if (cookie.Value != null)
                    {
                        Cookie.AppendFormat("{0}={1}; ", cookie.Key, cookie.Value);
                    }
                    else
                    {
                        Cookie.AppendFormat("{0}; ", cookie.Key);
                    }
                }
                Cookie.Length -= 2;
                result += Cookie.ToString() + "\n";
            }
            if (method == HTTPMethod.POST)
            {
                result += "\n";
                result += PostData+"\n";
            }
            //TODO:Add addition infomation
            /*
            Every new line are divided by '\r\n'
            The data MUST be ended with TWO '\r\n'
            */
            result += "\n";
            return result.Replace("\n", Environment.NewLine);
        }
    }

    public class HTTPResponse
    {
        public Dictionary<string, string> status;
        public Dictionary<string, string> header;
        public HTTPCookie cookie;
        public bool binaryBody;
        public int binaryDataEnds;
        public string body;
        public byte[] binaryData;
        public int bodyBytesLength;
        public int headerDataEnds;
        public HTTPRequest request;
        public HTTPRequest MakeRequest(HTTPMethod? method, string Url, object Addition, HTTPRequest.RequestCallback callback,HTTPCookie cookie=null,HTTPHeader additionHeader=null,string PostData=null)
        {
            Uri path = new Uri(Url);
            if (path.Host != request.Host)
            {
                HTTPOp.Request(method.Value, Url, Addition, callback,cookie,additionHeader,PostData);
                return null;
            }
            else
            {
                HTTPRequest newRequest = request.CopyTo();
                newRequest.method = method == null ? newRequest.method : method.Value;
                newRequest.Url = path.PathAndQuery;
                newRequest.Addition = Addition ?? newRequest.Addition ;
                newRequest.Callback = callback ?? newRequest.Callback ;
                //newRequest.Cookie = cookie ?? newRequest.Cookie ;
                newRequest.Cookie = this.cookie?.AddCookie(cookie).GetCookie(path.Host);
                newRequest.PostData = PostData;
                //newRequest.Header = header ?? newRequest.Header ;
                return newRequest;
            }
        }
        public HTTPResponse Parse(byte[] data)
        {
            if (header != null)
            {
                return this;
            }
            var stringData = Encoding.UTF8.GetString(data);
            this.headerDataEnds = ByteOp.PatternFind(data, new Byte[] { 0x0d, 0x0a, 0x0d, 0x0a });
            if (headerDataEnds == -1)
            {
                binaryBody = true;
                binaryData = data;
                binaryDataEnds = data.Length;
                return this;
            }
            var headerString = Encoding.UTF8.GetString(ByteOp.SubArray(data, 0, headerDataEnds - 0));
            this.binaryData = ByteOp.SubArray(data, headerDataEnds + 4, data.Length - headerDataEnds - 4);
            ParseHeader(headerString);
            ParseCookie();
            GetBody();
            return this;
        }
        public HTTPResponse ParseHeader(string data)
        {
            StringOp strop = new StringOp();
            strop.LoadText(data);
            String[] splitStatusLine = strop.GetLine(1).Split(' ');
            Dictionary<string, string> status = new Dictionary<String, String>{
                {"Protocol",splitStatusLine[0]},
                {"StatusCode",splitStatusLine[1]},
                {"StatusMessage",splitStatusLine[2]}
            };
            Dictionary<string, string> header = new Dictionary<String, String>();
            this.status = status;
            int line = 2;
            while (true)
            {
                String HeaderLine = strop.GetLine(line);
                if (HeaderLine == null)
                {
                    break;
                }
                String[] splitHeaderLine = HeaderLine.Split(new char[] { ':' }, 2);
                if (header.ContainsKey(splitHeaderLine[0]))
                {
                    header[splitHeaderLine[0]] += ";" + splitHeaderLine[1].Substring(1);
                }
                else
                {
                    header.Add(splitHeaderLine[0], splitHeaderLine[1].Substring(1));
                }
                line++;
            }
            this.header=header;
            return this;
        }
        public HTTPResponse ParseCookie()
        {
            var Header = this.header;
            if (!Header.ContainsKey("Set-Cookie"))
            {
                return null;
            }
            var ret = new HTTPCookie();
            var SetCookie = Header["Set-Cookie"];
            Header.Remove("Set-Cookie");
            ret._Domain = "";
            foreach (var cookie in SetCookie.Split(';'))
            {
                try
                {
                    var tmp = cookie.Split(new char[] { '=' }, 2);
                    var header = tmp[0];
                    var data = tmp[1];
                    if (header == " domain")
                    {
                        if (ret._Domain == "")
                        {
                            ret._Domain = data;
                        }
                        continue;
                    }
                    ret.AddCookie(header, data, ret._Domain);
                }
                catch (IndexOutOfRangeException e)
                {
                    ret.AddCookie(cookie, null, ret._Domain);
                }
            }
            return this;
        }
        public HTTPResponse GetBody()
        {
            this.bodyBytesLength = this.binaryData.Length;
            if (this.header.ContainsKey("Content-Type")&&ContentOp.isBinaryType(this.header["Content-Type"]))
            {
                if (this.header.ContainsKey("Content-Length"))
                {
                    var tmpData = this.binaryData;
                    this.binaryData = new Byte[int.Parse(this.header["Content-Length"])];
                    Array.Copy(tmpData, this.binaryData, tmpData.Length);
                    this.binaryDataEnds = tmpData.Length;
                }
                binaryBody = true;
            }
            else
            {
                binaryBody = false;
                this.body = Encoding.UTF8.GetString(this.binaryData);
            }
            return this;
        }

        public HTTPResponse AddBody(byte[] data)
        {
            this.bodyBytesLength += data.Length;
            if (this.binaryBody)
            {
                try
                {
                    Array.Copy(data, 0, this.binaryData, this.binaryDataEnds, data.Length);
                    this.binaryDataEnds += data.Length;
                }
                catch(System.ArgumentException e)
                {
                    Array.Resize(ref this.binaryData, this.binaryData.Length + data.Length);
                    Array.Copy(data, 0, this.binaryData, this.binaryDataEnds, data.Length);
                    this.binaryDataEnds += data.Length;
                }
            }
            else
            {
                this.body += Encoding.UTF8.GetString(data);
            }
            return this;
        }
    }

    public enum HTTPMethod
    {
        GET,
        POST
    }
}