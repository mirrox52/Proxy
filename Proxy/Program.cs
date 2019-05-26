using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Configuration;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Proxy
{
    class Program
    {

        static void Main(string[] args)
        {
            var listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 8080); // слушаем соединение
            listener.Start();
            while (true) 
            {
                var client = listener.AcceptTcpClient();
                Thread thread = new Thread(() => RecvData(client)); // и когда нам приходит запрос на подключение создаем новый поток
                thread.Start();
            }
        }

        public static void RecvData(TcpClient client) 
        {
            NetworkStream browser = client.GetStream();
            byte[] buf;
            buf = new byte[16000];
            while (true) // здесь мы читаем данные которые отправляет нам браузер и передаем их на обработку в httpserv
            {
                if (!browser.CanRead)
                    return;
                try
                {
                    browser.Read(buf, 0, buf.Length);
                }
                catch (IOException)
                {
                    return;
                }
                HTTPserv(buf, browser, client);
            }
        }

        public static void HTTPserv(byte[] buf, NetworkStream browser, TcpClient client)
        {
            try
            {
                string[] temp = Encoding.ASCII.GetString(buf).Trim().Split(new char[] { '\r', '\n' });
                
                string req = temp.FirstOrDefault(x => x.Contains("Host")); 
                req = req.Substring(req.IndexOf(":") + 2);
                string[] port = req.Trim().Split(new char[] { ':' }); // получаем хост, имя домена и номер порта (если есть)

                TcpClient server;
                if (port.Length == 2) // тут мы соединяемся с сервером по имени хоста и если есть порт в запросе то по порту, а если нет то по стандартному 80
                {
                    server = new TcpClient(port[0], int.Parse(port[1]));
                }
                else
                {
                    server = new TcpClient(port[0], 80);
                }

                NetworkStream servStream = server.GetStream(); // поток с сервером
                servStream.Write(buf, 0, buf.Length); // отправляем данные на сервер, которые получили от браузера
                var respBuf = new byte[32]; // для заголовка
                
               
                servStream.Read(respBuf, 0, respBuf.Length); // ответ от сервера

                browser.Write(respBuf, 0, respBuf.Length); // отправляем этот ответ браузеру

                string[] head = Encoding.UTF8.GetString(respBuf).Split(new char[] { '\r', '\n' }); // получаем код ответа
         
                string ResponseCode = head[0].Substring(head[0].IndexOf(" ") + 1);
                Console.WriteLine($"\n{req} {ResponseCode}");
                servStream.CopyTo(browser); // перенаправляем остальные данные от сервера к браузеру

            }
            catch
            {
                return;
            }
            finally
            {
                client.Dispose();
            }

        }

    }

}

