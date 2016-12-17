using System;
using System.Collections.Generic;

namespace KLib.Log
{
    public static class Log
    {
        public static int INFO = 0x01,WARNING=0x02,ERROR=0x04;
        private static int OrderNo = 0;
        private static Dictionary<int, string> PREFIX = new Dictionary<int, string>{
            {0x01,"INFO"},
            {0x02,"WARN"},
            {0x04,"ERR"}
        };
        public static void SetDisplayLevel(int displayLevel){
            DISPLAYLEVEL = displayLevel;
        }
        private static int DISPLAYLEVEL=0x01|0x02|0x04;
        public static bool displayTime = false;
        public static bool displaySource = false;
        private static Dictionary<int, ConsoleColor> COLOR = new Dictionary<int, ConsoleColor>{
            {0x01,ConsoleColor.Green},
            {0x02,ConsoleColor.Yellow},
            {0x04,ConsoleColor.Red}
        };
        private static LinkedList<LogRecord> bufferList=new LinkedList<LogRecord>();
        private static LogRecord currentRecord=new LogRecord();
        private static String PREFIXFORMAT = "[{0}]";
        private static DateTime startTime;
        public static void log(String Message,int Level,String source)
        {
            if (OrderNo == 0)
            {
                startTime = DateTime.Now;
            }
            OrderNo++;
            currentRecord.message = Message;
            currentRecord.level = Level;
            currentRecord.source = source;
            currentRecord.OrderNo = OrderNo;
            currentRecord.time = System.DateTime.Now;
            bufferList.AddLast(currentRecord);
            if((Level|DISPLAYLEVEL)==0){
                return;
            }
            Console.ForegroundColor = COLOR[Level];
            string displayMessage = "";
            if(displayTime){
                displayMessage += String.Format(PREFIXFORMAT, (currentRecord.time-startTime).ToString());
            }
            if(displaySource){
                displayMessage += String.Format(PREFIXFORMAT, currentRecord.source);
            }
            displayMessage+=(String.Format(PREFIXFORMAT, PREFIX[currentRecord.level])+Message);
            Console.WriteLine(displayMessage);
        }
    }

    public struct LogRecord{
        public int OrderNo;
        public System.DateTime time;
        public string source;
        public int level;
        public string message;
    }
}
