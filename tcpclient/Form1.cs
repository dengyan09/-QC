using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using System.Net.Sockets;//用到的TcpClient类与TcpListener类是两个专门用于TCP协议编程的类
using System.Net; //利用TcpClient类提供的方法，可以通过网络进行连接、发送和接收网络数据流
using System.Timers;
using System.Threading;
using System.IO;
using System.IO.Ports;
using System.Text.RegularExpressions;

namespace tcpclient
{
    public partial class client : Form
    {
        private string ipadd = "192.168.2.100";//Deng--默认本地（工控机的IP）;服务器指的是基恩士的DL-EN1，工控机是客户端
        private int port = 5555;//Deng--是否需要更改端口?
        TcpClient client01;//Deng--将 类TcpClient实例化为client01，
        NetworkStream netstream = null;//Deng--将 类NetworkStream实例化为netstream，并初始化为空（null）。。NetworkStream提供在阻止模式下通过 Stream 套接字发送和接收数据的方法
        private StreamReader strReader;//Deng--实现一个 TextReader，使其以一种特定的编码从字节流中读取字符。namespace System.IO
        private StreamWriter strWriter;//Deng--TextReader 为 StreamReader 和 StringReader 的抽象基类，它们分别从流和字符串读取字符。 使用这些派生类可打开一个文本文件以读取指定范围的字符，或基于现有的流创建一个读取器。 

        private Thread comportconthread = null;//串口连接线程
        private Thread Tcprecvthread = null;//TCP接收信息线程
        private Thread Tcpsendthread = null;//TCP发送信息线程
        private Thread Tcpserverthread = null;//TCP服务线程
       
        //private Thread comportsendthread = null;//串口发送线程

        //bool tag = true;//设置标志位，标志是否接收数据,当断开连接的时候，tag=false，表示不接收数据

        //private long batch_received_count = 0;
        private bool recv_OK = true;
        //private bool program_START = false;
        //private byte[] laser_send_chars = { M0 };

        public int count = 1;//计数的初始化 

        public bool H_ok;//用来判断是否接收到高电平信号。。。。

        float num1;//接收数据的代码中，代表测出的实际数值

        Thread t1;
        private delegate void FlushClient(); // 代理
        // 读取更新数据的文件变量
        StreamReader Data_File = null;
        // 文件的基础名称
        string file_base_name = "knife";
        // 文件编号
        int file_num = 0;
        // 用于存储数据的list
        List<double> chart_data_height = new List<double>();
        List<double> chart_x_index = new List<double>();

        // 用于采集统计数据的list
        List<double> delta_height = new List<double>();
        // 用于保存标准数据的list
        List<double> Standard_height = new List<double>();
        int standard_data_num = 0;
        double average_db = 0;  // 平均值
        double stdeval_db = 0;  // 标准差-但是目前输出的是方差


        bool tcp_SR = true;//8-1--判断TCP的发送与接收

        bool thread_exit = false;

        //-- 指示灯状态 --
        // 0 - 亮灰灯-停止工作
        // 1 - 亮绿灯-合格
        // 2 - 亮红灯-不合格
        // 3 - 黄灯闪烁-监测中
        // 4 - 亮蓝灯-保存文件
        //int lightcon = 0;
        bool toggle_light = false; // 是否需要黄灯闪烁
        int yellow_toggle = 0;
        //=======================================================
        private byte[] digital_output = new byte[32];
        private byte[] recv_buff = new byte[64];
        private int laser_data_index = 0;
        private long batch_received_count = 0;
        private SerialPort comm = new SerialPort();//实例化一个串口  
        private long portreceived_count = 0;//接收计数
        private byte[] digital_input = { 0x01, 0x02, 0x00, 0x00, 0x00, 0x04, 0x79, 0xC9 };//模块的主站发送指令        
        //private byte[] digital_output = {  };//模块的从站的正常响应报文指令


        private delegate void DelegateCallBackData(byte[] data); // Liu - 改成我需要的 byte[] 类型
        //Mouse check drawing
        private DelegateCallBackData delegateData = null;

        // 实际数据采集流程
        // 0. 定义低点
        // 0.1 IL 300
        // private double thresh_low = 300;
        // private double thresh_high = 350;
        // 0.2 IL 600
        //private double thresh_low = dist_near_lim;//const double dist_near_lim = 600; 探测区间上限
        //private double thresh_high = dist_far_lim;//const double dist_far_lim = 620; 探测区间下限
        // 1. 连续三点 - “三”这个数字需要视情况进行调整
        //private int continue3high = 0;
        //private int continue3low = 0;
        // 2. 数据缓冲区
        private double[] height_data = new double[512];
        private int height_data_num = 0;
        // 3. 数据采集开始标志位
        //private bool Sample_Start = false;
        // 4. 刀片数量编号，用来命名数据文件
        private int Knife_num = 0;

        private void client_Load(object sender, EventArgs e)
        {
            //初始化下拉串口名称列表框
            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports);
            combo_PortName.Items.AddRange(ports);
            combo_PortName.SelectedIndex = combo_PortName.Items.Count > 0 ? 0 : -1;
            combo_Baudrate.SelectedIndex = combo_Baudrate.Items.IndexOf("9600");

           

            //添加事件注册
            comm.DataReceived += comm_DataReceived;
            delegateData += comportReceived;


            //richTextBox1.Text = "M0";
            //richTextBox1.Text = "FR,01,037";
            button1.Enabled = true;
            button2.Enabled = false;
            send.Enabled = false;
            //richTextBox2.Text = "等待建立连接...\n";

            Control.CheckForIllegalCrossThreadCalls = false;//线程间操作无效: 从不是创建控件“richTextBox1”的线程访问它,线程委托

            // 注意,@的作用是"过滤转义字符",就是说\\可以写成\
            imageList1.Images.Add(Image.FromFile(@"..\..\PNG\Circle_Grey.png"));//读取--0--灰色图标的路径,注意更换地址
            imageList1.Images.Add(Image.FromFile(@"..\..\PNG\Circle_Green.png"));//读取--1--绿色图标的路径
            imageList1.Images.Add(Image.FromFile(@"..\..\PNG\Circle_Red.png"));//读取--2--红色图标的路径
            imageList1.Images.Add(Image.FromFile(@"..\..\PNG\Circle_Yellow.png"));//读取--3--黄色图标的路径
            imageList1.Images.Add(Image.FromFile(@"..\..\PNG\Circle_Blue.png"));//读取--4--蓝色图标的路径
            this.pictureBox_LED.Image = imageList1.Images[0];

            // 载入标准刀具文件 - 这里假设这个文件肯定有
            Standard_height.Clear();
            StreamReader sr_standard_file = new StreamReader("StandardKnife", false);
            string str_data = "";
            while (true)
            {
                str_data = sr_standard_file.ReadLine();
                if (null == str_data)
                    break;
                else
                {
                    Standard_height.Add(System.Convert.ToDouble(str_data));
                    standard_data_num++;
                }
            }
            sr_standard_file.Close();

            //===============Liu - 多线程-初始化线程===========
            t1 = new Thread(CrossThreadFlush);
            t1.IsBackground = true;
            t1.Start();
            //================================================
        }

        public client()
        {

            InitializeComponent();
            textBox1.Text = "192.168.2.99";//DL-EN1的IP是根据IP configurator设定的IP,
            textBox2.Text = "64000"; //DL-EN1的端口号也可以在IP configurator上进行修改，默认为64000

        }

        private void button1_Click(object sender, EventArgs e)//建立连接，启动串口、IP
        {
            comportconthread = new Thread(new ThreadStart(ComportConnection));
            comportconthread.Start();

            Tcpserverthread = new Thread(new ThreadStart(Connection));
            Tcpserverthread.Start();
            

        }

        private void send_Click(object sender, EventArgs e)
        {


        }

        private void button2_Click(object sender, EventArgs e)
        {
            richTextBox2.Text += "-----与服务器断开连接------\n";
            button2.Enabled = false;
            send.Enabled = false;
            button1.Enabled = true;
            textBox1.Enabled = true;
            textBox2.Enabled = true;
            try
            {
                Tcpsendthread.Abort();
                Tcprecvthread.Abort();
                Tcpserverthread.Abort();
                t1.Abort();
            }
            catch (Exception a)
            {
                Console.WriteLine(a);
            }
            comm.Close();//dy---串口关闭
            this.timer1.Stop();
            netstream.Dispose();
            netstream.Close();
            client01.Close();
        }


        private void ComportConnection()   //连接串口
        {

            comm.PortName = combo_PortName.Text;
            //comm.BaudRate = int.Parse(combo_Baudrate.Text);

            // ------------------------Liu 打开失败要停止流程防止错误 --------------------------
            try
            {
                comm.Open();
            }
            catch (Exception ex)
            {
                //创建一个新的comm对象
                comm = new SerialPort();
                //异常信息
                MessageBox.Show(ex.Message);
                return;
            }
            richTextBox2.Text += "------串口已连接------\n";
            comm.Write(digital_input, 0, 8);
            this.timer1.Start();

        }


        void comm_DataReceived(object sender, SerialDataReceivedEventArgs e)//串口接收信息
        {
            int n = comm.BytesToRead;

            portreceived_count += n;//增加接收计数
            comm.Read(recv_buff, 0, n);//读取缓冲数据

            // Liu - Plan B 无脑收，只管是不是该清空了
            batch_received_count += n;
            for (int i = 0; i < n; i++)
            {
                //System.Diagnostics.Debug.WriteLine("###DEBUG### laser_data_index is {0}", laser_data_index);
                digital_output[laser_data_index++] = recv_buff[i];
            }

        }

        // 添加定时器1，判断串口是否接收完整---01，02，01，08，A0，4E--- 
        private void timer1_Tick(object sender, EventArgs e)//在timer1的属性中，一定要把事件timer1_Tick激活，此事件是隔多久访问一次
        {
            // 添加数据处理、解析部分
            
            if (batch_received_count == 6)//判断是否接受了6位数据---01，02，01，08，A0，4E---
            {
                batch_received_count = 0;
                laser_data_index = 0;
                comportReceived(digital_output); // 这个函数里，开始关中断、结束开中断，保证了时序合理。

            }
            else if (recv_OK == false)
            {
                System.Diagnostics.Debug.WriteLine("Timer1 -- receive not OK!");
                //program_START = false;
            }
        }
        private void comportReceived(byte[] digital_output)//判断好串口接收到完整的数据，进行数据处理
        {
            this.timer1.Stop();           
            H_ok = digital_output[3] == 0x08;
            if (H_ok)
                System.Diagnostics.Debug.WriteLine("=============H_ok is high");
            else
                System.Diagnostics.Debug.WriteLine("=============H_ok is low");
            //H_ok是true，就是接收到高电平，H_ok是false，就是低电平；    

            this.timer1.Start();
            comm.Write(digital_input, 0, 8);
            return;
            
        }

            
       
        
        private void Connection()   //连接tcp-服务器的方法
        {
            try
            {
                IPAddress ipaddress = IPAddress.Parse(textBox1.Text);
                ipadd = Convert.ToString(ipaddress);
                port = Convert.ToInt32(textBox2.Text);
                //richTextBox2.Text = "Try to connect to " + ipaddress + ":" + port + "...\n";
                client01 = new TcpClient(ipadd, port);

                netstream = client01.GetStream();//返回用于发送和接收的数据流
                strReader = new StreamReader(netstream);
                strWriter = new StreamWriter(netstream);

                Tcpsendthread = new Thread(new ThreadStart(senddata));
                Tcpsendthread.Start();

                Tcprecvthread = new Thread(new ThreadStart(recvdata)); //创建接收信息线程，并启动
                Tcprecvthread.Start();
                //richTextBox2.Text += "------与主机" + ipaddress + ":" + port + "建立连接----\n";
                richTextBox2.Text += "------与服务器建立连接------\n";

                //netstream.Close();
                //client.Close();

                //获取本地的IP和本地端口
                IPEndPoint localIP1 = (IPEndPoint)client01.Client.LocalEndPoint;
                //MessageBox.Show(localIP1.Address.ToString());//本地IP,弹框显示 
                label3.Text = "本地端口：" + localIP1.Port.ToString() + "";//本地端口
                label4.Text = "本地IP：" + localIP1.Address.ToString() + "";//本地IP     

                button1.Enabled = false;
                button2.Enabled = true;
                send.Enabled = true;
                //tag = true;
                textBox1.Enabled = false;
                textBox2.Enabled = false;
            }
            catch (Exception e)
            {
                //MessageBox.Show("连接目标主机被拒绝");
                //richTextBox2.Text += "连接目标主机被拒绝\n";
                //MessageBox.Show(e.Message, "提示");
                Console.WriteLine(e);
            }
        }

        private void senddata() //tcp-发送数据
        {
            while ( true )
            {
                //System.Diagnostics.Debug.WriteLine("===Send is return!===");
                /*
                if (H_ok == true )
                {
                    if (tcp_SR)
                    {
                        tcp_SR = false;
                        //int i;
                        //for (i = 0; i < count; i++)
                        //{
                        try
                        {
                            string txtContent = "M0";
                            strWriter.WriteLine(txtContent);//往当前的数据流中写入一行字符串
                            richTextBox2.Text += "" + System.DateTime.Now.ToLongTimeString() + "指令：" + txtContent + "\n";
                            strWriter.Flush();//刷新当前数据流中的数据，释放网络流对象                                      
                            Thread.Sleep(1000);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "异常操作！");
                        }
                        // }
                    }
                }
                else
                {
                    ;
                ;
                    //richTextBox2.Text += "------现在无刀具------\n";
                // System.Diagnostics.Debug.WriteLine("===Send is return!===");
                }
                */
                if (tcp_SR)
                {
                    tcp_SR = false;//先关了bool判断
                    
                    try
                    {
                        string txtContent = "M0";
                        strWriter.WriteLine(txtContent);//往当前的数据流中写入一行字符串
                       // richTextBox2.Text += "" + System.DateTime.Now.ToLongTimeString() + "指令：" + txtContent + "\n";
                        strWriter.Flush();//刷新当前数据流中的数据，释放网络流对象                                      
                        Thread.Sleep(100);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "异常操作！");
                    }
                    
                }

            }
        }


        private void recvdata()  //tcp-接收数据
        {                       
            byte[] bytes = new byte[1024];           
            int bytesRead = 0;                      
           while ( true )
           {
                //if ( H_ok == true )
                //{
                //    //tcp_SR = false;//tcp-发送端先不发送
                //    try
                //    {
                //        bytesRead = netstream.Read(bytes, 0, bytes.Length);
                //    }
                //     catch (Exception)
                //    {
                //        System.Diagnostics.Debug.WriteLine("===read failed!===");
                //        break;
                //    }
                //    if (bytesRead == 0)
                //    {
                //        System.Diagnostics.Debug.WriteLine("===nothing read!===");
                //        break;
                //    }

                //    string message = System.Text.Encoding.UTF8.GetString(bytes, 0, bytesRead);
                //    richTextBox2.Text += "------Port data right------\n";

                //    //dy--------------------M0测量值指令----------------
                //    string[] sArray;
                //     //接收数据是否存在"M0,"
                //    if (message.Contains("M0,"))
                //    {
                //        //得到"M0,"以外字串
                //        sArray = message.Split(new string[] { "M0," }, StringSplitOptions.RemoveEmptyEntries);
                //         //去掉字串中的空格
                //        string temp = sArray[0];
                //        float num = float.Parse(temp);
                //        num1 = (600 - num / 100);//激光器的型号，IL300----600                   
                //        //num1 = (300 - num / 100);
                //        richTextBox2.Text += "" + System.DateTime.Now.ToLongTimeString() + "测量值：" + num1 + "\n";//输出返回消息
                //        // 黄灯闪烁表示测量开始
                //        toggle_light = true;
                //        Flash_Yellow_LED();

                //        //if ( num1 < 620 && num1 > 600 )//数据的上下限---7-31-不要上下限，只需要高低电平来判断
                //        //{
                //        count++;

                //        if (height_data_num < 512)//给测量的数据设了上限，512
                //            height_data[height_data_num++] = num1;
                //        else
                //            height_data_num = 512;

                //        tcp_SR = true;
                //    }
                //    //tcp_SR = true;//tcp-发送端继续发送
                //}
                //else
                //{
                //    System.Diagnostics.Debug.WriteLine("===Saving!===");
                //     if (height_data_num != 0) //dy----H_ok == false表示测量低电平,开始保存起来--7-31 //tcp_SR = false;//不发送TCP--8-1
                //     {
                //        StreamWriter sw = new StreamWriter("knife" + Knife_num, false);//7-31--数据直接保存，只要在高电平，08里面。 
                //        for (int i = 0; i < height_data_num; i++)
                //        {
                //           sw.WriteLine("{0}", height_data[i]);
                //        }
                //        Knife_num++;
                //        sw.Flush();
                //        sw.Close();
                //        height_data_num = 0;
                //        pictureBox_LED.Image = imageList1.Images[4];//保存文件显示蓝色指示灯

                //     }
                //}
                
                    //tcp_SR = false;//tcp-发送端先不发送
                try
                {
                    bytesRead = netstream.Read(bytes, 0, bytes.Length);
                }
                catch (Exception)
                {
                    System.Diagnostics.Debug.WriteLine("===read failed!===");
                    break;
                }
                if (bytesRead == 0)
                {
                    System.Diagnostics.Debug.WriteLine("===nothing read!===");
                    break;
                }

                string message = System.Text.Encoding.UTF8.GetString(bytes, 0, bytesRead);
                //richTextBox2.Text += "------Port data right------\n";

                //dy--------------------M0测量值指令----------------
                string[] sArray;
                //接收数据是否存在"M0,"
                if (message.Contains("M0,"))
                {
                    //得到"M0,"以外字串
                    sArray = message.Split(new string[] { "M0," }, StringSplitOptions.RemoveEmptyEntries);
                    //去掉字串中的空格
                    string temp = sArray[0];
                    float num = float.Parse(temp);
                    num1 = (600 - num / 100);//激光器的型号，IL300----600                   
                    //num1 = (300 - num / 100);
                    //richTextBox2.Text += "" + System.DateTime.Now.ToLongTimeString() + "测量值：" + num1 + "\n";//输出返回消息
                    // 黄灯闪烁表示测量开始
                    toggle_light = true;
                    Flash_Yellow_LED();

                    //if ( num1 < 620 && num1 > 600 )//数据的上下限---7-31-不要上下限，只需要高低电平来判断
                    //{
                    //count++;

                    if (H_ok)
                    {
                        if (height_data_num < 512)//给测量的数据设了上限，512
                            height_data[height_data_num++] = num1;
                        else
                            height_data_num = 512;
                    }
                    else
                    {
                        if (height_data_num != 0) //dy----H_ok == false表示测量低电平,开始保存起来--7-31 //tcp_SR = false;//不发送TCP--8-1
                        {
                            System.Diagnostics.Debug.WriteLine("===Saving!===");
                            StreamWriter sw = new StreamWriter("knife" + Knife_num, false);//7-31--数据直接保存，只要在高电平，08里面。 
                            for (int i = 0; i < height_data_num; i++)
                            {
                                sw.WriteLine("{0}", height_data[i]);
                            }
                            Knife_num++;
                            sw.Flush();
                            sw.Close();
                            height_data_num = 0;
                            pictureBox_LED.Image = imageList1.Images[4];//保存文件显示蓝色指示灯
                            richTextBox2.Text += "" + System.DateTime.Now.ToLongTimeString() + "数据已保存" + Knife_num + "\n";
                        }
                    }
                    tcp_SR = true;
                }
                //tcp_SR = true;//tcp-发送端继续发送                             
            }

        }       
             
        //============================多线程相关函数=======================================
        private void CrossThreadFlush()
        {
            while (false == thread_exit)
            {
                //将sleep和无限循环放在等待异步的外面
                // liu- 看起来最终会无限循环这里面的内容了
                Thread.Sleep(1000);

                Flash_Yellow_LED();

                // 判断文件是否存在
                if (!File.Exists(file_base_name + file_num))
                    continue;
                Data_File = null;
                // 判断文件是否被占用
                try
                {
                    Data_File = new StreamReader(file_base_name + file_num, false);
                }
                catch
                {
                    continue;
                }

                // 如果以上两步通过，说明文件存在，开始读取
                file_num++; // 文件编号自增，下次不读这次的了
                chart_data_height.Clear();
                chart_x_index.Clear();
                delta_height.Clear();
                string str = "";
                double x_index = 0;
                while (true)
                {
                    str = Data_File.ReadLine();
                    if (null == str)
                        break;
                    else
                    {
                        chart_data_height.Add(System.Convert.ToDouble(str));
                        // chart_x_index.Add(x_index); // 这回要画的不是绝对高度是相对误差量
                        x_index = x_index + 1;
                    }
                }
                Data_File.Close();
                // 计算均值
                int loop_max = System.Math.Min((int)(x_index), standard_data_num);
                for (int i = 0; i < loop_max; i++)
                {
                    delta_height.Add(chart_data_height[i] - Standard_height[i]);
                    chart_x_index.Add((double)(i));
                    average_db = average_db + delta_height[i];
                }
                average_db = average_db / (double)(loop_max);
                average_db = Math.Round(Convert.ToDouble(average_db), 2, MidpointRounding.AwayFromZero);//dy------保留2位小数点
                // 计算方差
                for (int i = 0; i < loop_max; i++)
                {
                    stdeval_db = stdeval_db + (delta_height[i] - average_db) * (delta_height[i] - average_db);
                }
                stdeval_db = stdeval_db / (double)(loop_max); // 这是方差
                stdeval_db = Math.Round(Convert.ToDouble(stdeval_db), 2, MidpointRounding.AwayFromZero);//dy------保留2位小数点
                // stdeval_db = System.Math.Sqrt(stdeval_db); // 这是标准差

                

                // 开始绘制
                ThreadFunction();
            }
        }
        private void ThreadFunction()
        {
            if (this.textBox4.InvokeRequired)//等待异步 
            {
                FlushClient fc = new FlushClient(ThreadFunction);
                this.Invoke(fc);//通过代理调用刷新方法 
            }
            else
            {
                // this.textBox1.Text = DateTime.Now.ToString();
                chart1.Series[0].Points.DataBindXY(chart_x_index, delta_height);
                textBox4.Text = average_db.ToString();
                textBox3.Text = stdeval_db.ToString();

                if (average_db < 1)//直接判断均值的范围，dy---7-31--质量判定
                    pictureBox_LED.Image = imageList1.Images[1];
                else
                    pictureBox_LED.Image = imageList1.Images[2];
            }
        }

        private void Flash_Yellow_LED()
        {
            // 灯是否要变
            if (toggle_light)
            {
                if (this.pictureBox_LED.InvokeRequired)//等待异步 
                {
                    FlushClient fc = new FlushClient(Flash_Yellow_LED);
                    this.Invoke(fc);//通过代理调用刷新方法 
                }
                else
                {
                    if (0 == yellow_toggle)
                    {
                        pictureBox_LED.Image = imageList1.Images[3];
                        yellow_toggle = 1;
                    }
                    else
                    {
                        pictureBox_LED.Image = imageList1.Images[0];
                        yellow_toggle = 0;
                    }
                }
            }
        }      
    }
}
