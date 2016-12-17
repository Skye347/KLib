using System;
using System.Text;
using System.Net.Sockets;
using KLib.NetCore;
using KLib.HTTP;
using KLib.Log;
using HtmlAgilityPack;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        Log.displayTime = true;
        HTTPOp.Init();
        //EchoProtocolCallback.StartEchoServer("127.0.0.1", 1010);
        //EchoProtocolCallback.StartEchoClient("127.0.0.1", 1010,"a hi",false);
        //EchoProtocolCallback.StartEchoClient("127.0.0.1", 1010, "b hi",false);
        HTTPOp.Request(HTTPMethod.POST, "http://httpbin.org/post",null, RequestCallback);
        System.Console.ReadLine();
    }

    static int count;

    static HTTPRequest RequestCallback(HTTPResponse response)
    {
        Log.log(count.ToString(), 1, "RequestCallback");
        Log.log(response.body.Length.ToString(), 1, "RequestCallback");
        //HtmlDocument doc = new HtmlDocument();
        //doc.LoadHtml(response.body);
        //Log.log(response.body, Log.INFO, "RequestCallback");
        //var node = doc.DocumentNode.SelectNodes("//*[@id='IndustryNews']/ul[1]/li[8]/a");
        //foreach (var item in node)
        //{
        //    Log.log(item.InnerText, Log.INFO, "RequestCallback");
        //    File.WriteAllText("E://test.txt", item.InnerText);
        //}
        count++;
        return response.MakeRequest(HTTPMethod.GET, "http://www.oschina.net/", null, RequestCallback).ClearHeader();
    }
}

public class EchoProtocolCallback : KLib.NetCore.Callback.CallbackCollection
{
    public string data;
    public bool displayReceive=true;
    int Count = 0;
    override public object Received(byte[] data, Socket socket, out SocketException err, object Addition = null)
    {
        if (displayReceive)
        {
            Console.WriteLine(Count.ToString() + ":" + Encoding.ASCII.GetString(data));
        }
        Count++;
        Write(data, socket, out err);
        return null;
    }
    override public object Connected(Socket socket, out SocketException err)
    {
        Write(Encoding.ASCII.GetBytes(data), socket, out err);
        return null;
    }
    public static void StartEchoServer(String ip, int port)
    {
        EchoProtocolCallback EchoCallback = new EchoProtocolCallback();
        EchoCallback.UseTcp();
        Core EchoServerCore = new AsynCore();
        EchoServerCore.SetServer(ip, port, EchoCallback,10);
        EchoServerCore.StartListen();
        EchoServerCore.Run();
    }

    public static void StartEchoClient(String ip, int port,string data,bool displayReceive)
    {
        EchoProtocolCallback EchoCallback = new EchoProtocolCallback();
        EchoCallback.data = data;
        EchoCallback.displayReceive = displayReceive;
        EchoCallback.UseTcp();
        Core EchoClientCore = new AsynCore();
        EchoClientCore.SetClient(EchoCallback, true);
        EchoClientCore.Connect(ip, port);
        EchoClientCore.Run();
    }
}