using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Server
{
    public partial class Form1 : Form
    {
        TcpListener server;
        TcpClient client;
        NetworkStream stream;
        OpenFileDialog openFileDialog = new OpenFileDialog();

        [Obsolete]
        public Form1() => InitializeComponent();

        [Obsolete]
        private void listen_Click(object sender, EventArgs e)
        {
            Task.Run(() => StartServer());
        }

        private async Task StartServer()
        {
            server = new TcpListener(System.Net.IPAddress.Any, 9050);
            server.Start();
            AppendToChatBox("Server started. Waiting for connections...");

            while (true)
            {
                client = await server.AcceptTcpClientAsync();
                AppendToChatBox("Client connected.");
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            {
                stream = client.GetStream();

                byte[] welcomeMessage = Encoding.ASCII.GetBytes("Welcome to the server!\n");
                await stream.WriteAsync(welcomeMessage, 0, welcomeMessage.Length);

                byte[] buffer = new byte[1024];
                while (true)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    string receivedMessage = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    if (receivedMessage.StartsWith("dir:"))
                    {
                        await SendFilesDirectoriesAsync(receivedMessage);
                    }
                    else if (receivedMessage.StartsWith("file:"))
                    {
                        await HandleFileRequestAsync(receivedMessage.Substring(5).Trim());
                    }
                    else if (receivedMessage.StartsWith("img:"))
                    {
                        await HandleImageRequestAsync(receivedMessage.Substring(4).Trim());
                    }
                    else if (receivedMessage.StartsWith("video:"))
                    {
                        await HandleVideoRequestAsync(receivedMessage.Substring(6).Trim());
                    }
                    else if (receivedMessage.StartsWith("web:"))
                    {
                        await HandleWebRequestAsync(receivedMessage.Substring(4).Trim());
                    }
                    else
                    {
                        AppendToChatBox($"Received: {receivedMessage}");
                    }
                }

                AppendToChatBox("Client disconnected.");
            }
        }

        private async Task HandleWebRequestAsync(string link)
        {
            WebClient wc = new WebClient();

            byte[] data = Encoding.ASCII.GetBytes("WEB:");
            await stream.WriteAsync(data, 0, data.Length);

            byte[] response = wc.DownloadData(link);
            byte[] sizeData = BitConverter.GetBytes(response.Length);

            await stream.WriteAsync(sizeData, 0, sizeData.Length);
            await stream.WriteAsync(response, 0, response.Length);
            ////////////
            string filename = "webpage.htm";
            wc.DownloadFile(link, filename);
            Console.WriteLine(Encoding.ASCII.GetString(response));
        }

        private async Task HandleFileRequestAsync(string fileName)
        {
            if (File.Exists(fileName))
            {
                byte[] data = Encoding.ASCII.GetBytes("File Content:");
                await stream.WriteAsync(data, 0, data.Length);

                string compressedFilePath = CompressFile(fileName);
                byte[] content = File.ReadAllBytes(compressedFilePath);

                byte[] sizeData = BitConverter.GetBytes(content.Length);
                await stream.WriteAsync(sizeData, 0, sizeData.Length);
                await stream.WriteAsync(content, 0, content.Length);
            }
            else
            {
                byte[] data = Encoding.ASCII.GetBytes("File Not Exist");
                await stream.WriteAsync(data, 0, data.Length);
            }
        }

        private async Task HandleImageRequestAsync(string imgName)
        {
            byte[] data = Encoding.ASCII.GetBytes("img");
            await stream.WriteAsync(data, 0, data.Length);
            if (File.Exists(imgName))
            {
                Invoke((Action)(() => pictureBox1.ImageLocation = imgName));
                string compressedFilePath = CompressFile(imgName);
                byte[] content = File.ReadAllBytes(compressedFilePath);
                byte[] sizeData = BitConverter.GetBytes(content.Length);

                await stream.WriteAsync(sizeData, 0, sizeData.Length);
                await stream.WriteAsync(content, 0, content.Length);
            }
            else
            {
                data = Encoding.ASCII.GetBytes("File Not Exist");
                await stream.WriteAsync(data, 0, data.Length);
            }
        }

        private async Task HandleVideoRequestAsync(string videoName)
        {
            byte[] data = Encoding.ASCII.GetBytes("video");
            await stream.WriteAsync(data, 0, data.Length);
            if (File.Exists(videoName))
            {
                string compressedFilePath = CompressFile(videoName);
                byte[] content = File.ReadAllBytes(compressedFilePath);
                byte[] sizeData = BitConverter.GetBytes(content.Length);

                await stream.WriteAsync(sizeData, 0, sizeData.Length);
                await stream.WriteAsync(content, 0, content.Length);

                Invoke((Action)(() =>
                {
                    axWindowsMediaPlayer1.URL = videoName;
                }));
            }
            else
            {
                data = Encoding.ASCII.GetBytes("File Not Exist");
                await stream.WriteAsync(data, 0, data.Length);
            }
        }

        private async Task SendFilesDirectoriesAsync(string receivedMessage)
        {
            string directoryName = receivedMessage.Substring(4).Trim();
            DirectoryInfo directory = new DirectoryInfo(directoryName);
            if (directory.Exists)
            {
                var files = directory.GetFiles();
                foreach (var file in files)
                {
                    byte[] data = Encoding.ASCII.GetBytes("file: " + file + Environment.NewLine);
                    await stream.WriteAsync(data, 0, data.Length);
                }

                var folders = directory.GetDirectories();
                foreach (var folder in folders)
                {
                    byte[] data = Encoding.ASCII.GetBytes("folder: " + folder + Environment.NewLine);
                    await stream.WriteAsync(data, 0, data.Length);
                }
            }
            else
            {
                byte[] errorMessage = Encoding.ASCII.GetBytes("Directory not found\n");
                await stream.WriteAsync(errorMessage, 0, errorMessage.Length);
            }
        }

        private async void send_Click(object sender, EventArgs e)
        {
            try
            {
                string message = messageBox.Text;
                byte[] data = Encoding.ASCII.GetBytes(message);
                messageBox.Text = string.Empty;
                await stream.WriteAsync(data, 0, data.Length);
                AppendToChatBox($"Sent to client: {message}");
            }
            catch (Exception ex)
            {
                AppendToChatBox($"Error sending data to clients: {ex.Message}");
            }
        }

        private string CompressFile(string filePath)
        {
            string compressedFilePath = filePath + ".gz";
            using (FileStream originalFileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (FileStream compressedFileStream = new FileStream(compressedFilePath, FileMode.Create, FileAccess.Write))
            using (GZipStream compressionStream = new GZipStream(compressedFileStream, CompressionMode.Compress))
            {
                originalFileStream.CopyTo(compressionStream);
            }
            return compressedFilePath;
        }

        private void AppendToChatBox(string message)
        {
            if (chatBox.InvokeRequired)
            {
                chatBox.Invoke(new Action(() => chatBox.AppendText(message + Environment.NewLine)));
            }
            else
            {
                chatBox.AppendText(message + Environment.NewLine);
            }
        }

        private async void Open_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                byte[] data = Encoding.ASCII.GetBytes("img");
                await stream.WriteAsync(data, 0, data.Length);
                if (File.Exists(openFileDialog.FileName))
                {
                    Invoke((Action)(() => pictureBox1.ImageLocation = openFileDialog.FileName));
                    string compressedFilePath = CompressFile(openFileDialog.FileName);
                    byte[] content = File.ReadAllBytes(compressedFilePath);
                    byte[] sizeData = BitConverter.GetBytes(content.Length);

                    await stream.WriteAsync(sizeData, 0, sizeData.Length);
                    await stream.WriteAsync(content, 0, content.Length);
                }
                else
                {
                    data = Encoding.ASCII.GetBytes("File Not Exist");
                    await stream.WriteAsync(data, 0, data.Length);
                }
            }
        }
    }
}
