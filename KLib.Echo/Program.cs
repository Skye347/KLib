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
        //HTTPOp.Init();
        EchoProtocolCallback.StartEchoServer("127.0.0.1", 1010);
        //EchoProtocolCallback.StartEchoClient("127.0.0.1", 55056, "a hi", false);
        //EchoProtocolCallback.StartEchoClient("127.0.0.1", 1010, "b hi", false);
        //HTTPOp.Request(HTTPMethod.GET, "http://www.oschina.net/",null, RequestCallback);
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
    override public object Received(byte[] data, UniNetObject connection, out KLib.NetCore.Error.NetCoreException err, object Addition = null)
    {
        if (displayReceive)
        {
            Log.log(Count.ToString() + ":" + Encoding.ASCII.GetString(data),Log.INFO,"EchoReceived");
        }
        Count++;
        connection.Write(data, out err);
        return null;
    }
    override public object Connected(UniNetObject connection, out KLib.NetCore.Error.NetCoreException err)
    {
        connection.Write(Encoding.ASCII.GetBytes(data), out err);
        return null;
    }
    public static void StartEchoServer(String ip, int port)
    {
        EchoProtocolCallback EchoCallback = new EchoProtocolCallback();
        EchoCallback.UseUdp();
        Core EchoServerCore = new UniAsynCore();
        EchoServerCore.SetServer(ip, port, EchoCallback,10);
        EchoServerCore.StartListen();
        EchoServerCore.Run();
    }

    public static void StartEchoClient(String ip, int port,string data,bool displayReceive)
    {
        EchoProtocolCallback EchoCallback = new EchoProtocolCallback();
        EchoCallback.data = data;
        EchoCallback.displayReceive = displayReceive;
        EchoCallback.UseUdp();
        Core EchoClientCore = new UniAsynCore();
        EchoClientCore.SetClient(EchoCallback, true);
        EchoClientCore.Connect(ip, port);
        EchoClientCore.Run();
    }
}