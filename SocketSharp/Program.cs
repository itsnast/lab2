using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace SocketSharp
{
    class Program
    {
        static int clientId = 0;
        private static Mutex mut = new Mutex();

        enum Messages
        {
            M_INIT,
            M_EXIT,
            M_GETDATA,
            M_NODATA,
            M_DATA,
            M_CONFIRM,
        };

        enum Members
        {
            M_BROKER = 0,
            M_ALL = 10,
            M_USER = 100
        };

        public struct MsgHeader
        {
            public int m_To;
            public int m_From;
            public int m_Type;
            public int m_Size;

            public MsgHeader(int m_To, int m_From, int m_Type, int m_Size)
            {
                this.m_To = m_To;
                this.m_From = m_From;
                this.m_Type = m_Type;
                this.m_Size = m_Size;
            }

            public void Send(ref Socket s)
            {
                s.Send(BitConverter.GetBytes(m_To), sizeof(int), SocketFlags.None);
                s.Send(BitConverter.GetBytes(m_From), sizeof(int), SocketFlags.None);
                s.Send(BitConverter.GetBytes(m_Type), sizeof(int), SocketFlags.None);
                s.Send(BitConverter.GetBytes(m_Size), sizeof(int), SocketFlags.None);
            }
        };


        public class Message
        {
            public MsgHeader m_Header;
            public string m_Data;
            public static int m_ClientID;
            Message()
            {
                m_Header = new MsgHeader(0, 0, 0, 0);
            }

            Message(int To, int From, int Type = (int)Messages.M_DATA, string Data = "")
            {
                m_Header = new MsgHeader(To, From, Type, Data.Length);
                m_Data = Data;
            }

            public void Send(ref Socket s, int To, int From, int Type, string Data)
            {
                Message m = new Message(To, From, Type, Data);
                m.Send(ref s);
            }
            public void Send (ref Socket s)
            {
                m_Header.Send(ref s);
                if (m_Header.m_Size > 0)
                {
                    s.Send(cp866.GetBytes(m_Data), m_Data.Length, SocketFlags.None);
                }
            }

            public static Message Send (int To, int Type, string Data = "")
            {
                int nPort = 12345;
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), nPort);
                Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                s.Connect(endPoint);

                Message m = new Message(To, clientId, Type, Data);
                if (s.Connected)
                {
                    m.Send(ref s);
                    m.Receive(ref s);
                }
                if (m.m_Header.m_Type == (int)Messages.M_INIT && clientId == 0)
                {
                    clientId = m.m_Header.m_To;
                }
                return m;
            }

            public int ReceiveHeaders(Socket s)
            {
                byte[] b = new byte[sizeof(int)];
                s.Receive(b, sizeof(int), SocketFlags.None);
                return BitConverter.ToInt32(b, 0);
            }
            public int Receive(ref Socket s)
            {
                m_Header.m_To = ReceiveHeaders(s);
                m_Header.m_From = ReceiveHeaders(s);
                m_Header.m_Type = ReceiveHeaders(s);
                m_Header.m_Size = ReceiveHeaders(s);

                if (m_Header.m_Size > 0)
                {
                    byte[] b = new byte[m_Header.m_Size];
                    s.Receive(b, m_Header.m_Size, SocketFlags.None);
                    m_Data = cp866.GetString(b, 0, m_Header.m_Size);
                }
                return m_Header.m_Type;
            }

        }
        public static void receiveMessage()
        {
            while (true)
            {
                Message m = Message.Send((int)Members.M_BROKER, (int)Messages.M_GETDATA);
                switch (m.m_Header.m_Type)
                {
                    case (int)Messages.M_DATA:
                        {
                            mut.WaitOne();
                            Console.WriteLine(m.m_Data);
                            mut.ReleaseMutex();
                            break;
                        }
                    default:
                        {
                            Thread.Sleep(500);
                            break;
                        }
                }   
            }
        }

        static Encoding cp866 = Encoding.GetEncoding("CP866");

        static void Main(string[] args)
        {
            Thread t = new Thread(() => receiveMessage());
            t.Start();
            Message m = Message.Send((int)Members.M_BROKER, (int)Messages.M_INIT);

            while (true)
            {
                Console.WriteLine("1. Send to client\n2. Send to ALL\n3. Exit");
                int n;
                n = int.Parse(Console.ReadLine());
                switch (n)
                {
                    case 1:
                            {
                            mut.WaitOne();
                            Console.WriteLine("Enter client Id");
                                int id;
                                id = int.Parse(Console.ReadLine());
                                Console.WriteLine("Enter message text");
                                string str;
                                str = Console.ReadLine();
                            mut.ReleaseMutex();
                            Message.Send(id, (int)Messages.M_DATA, str);
                                break;
                            }
                    case 2:
                        {
                            mut.WaitOne();
                            Console.WriteLine("Enter message text");
                            string str;
                            str = Console.ReadLine();
                            mut.ReleaseMutex();
                            Message.Send((int)Members.M_ALL, (int)Messages.M_DATA, str);
                            break;
                        }
                    case 3:
                        {
                            Message.Send((int)Members.M_BROKER, (int)Messages.M_EXIT, "");
                            return;
                        }

                }   
                
            }
        }
    }
}
