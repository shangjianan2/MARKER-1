using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.IO;
using System.IO.Ports;
using System.Security.Permissions;
using System.Threading;
using System.Windows;
using System.Windows.Forms.DataVisualization.Charting;
using System.Collections;

using System.Diagnostics;

using Excel = Microsoft.Office.Interop.Excel;

namespace MARKER
{
    public partial class Form1 : Form
    {
        private bool recStaus = true;//接收状态字
        Queue recQueue = new Queue();//接收数据过程中，接收数据线程与数据处理线程直接传递的队列，先进先出
        System.Threading.Timer timer1 = null;//定时刷新串口信息

        #region
        int enableordisable_button_flag = 0;
        int enableordisable_button_flag_old = 0;

        const int ALLBUTTON_DISABLE = 0x0000;
        const int ALLBUTTON_ENABLE = 0xffff;

        const int OPENSERIAL_BUTTON = 0x0001;
        const int CLEARSERIAL_BUTTON = 0x0002;
        const int READXINXI_BUTTON = 0x0004;
        const int READSHUJU_BUTTON = 0x0008;

        const int SAVESHUJU_BUTTON = 0x0010;
        const int JIEXISHUJU_BUTTON = 0x0020;
        const int SHENGCHENGBAOBIAO_BUTTON = 0x0040;
        const int SAVEIMAGE_BUTTON = 0x0080;

        const int CLEARFLASH_BUTTON = 0x0100;
        const int DOWNLOAD_BUTTON = 0x0200;
        const int WRITEXULIEHAO_BUTTON = 0x0400;
        const int WRTIECHUFAYUZHI_BUTTON = 0x0800;

        const int WRITEGONGZUOMOSHI_BUTTON = 0x1000;
        const int WRITECICHUFAYUZHI_BUTTON = 0x2000;
        #endregion

        //为了方便基本数据的显示，此处设置了一些全局变量
        #region
        float shengyukongjian_dbl = 0;
        int shangchuanzishu_int = 0;
        int chufacishu_int = 0;
        string yingjianbanben_str = null;
        string gujianbanben_str = null;
        float dianchidianliang_dbl = 0;
        string xuliehao_str = null;
        int gongzuomoshi = 0;
        float chufayuzhi_dbl = 0;
        float cichufayuzhi_dbl = 0;
        #endregion
        public Form1()
        {
            InitializeComponent();
        }
        private void Form1_Load(object sender, System.EventArgs e)//考虑到大多数程序的初始化都放在加载函数中，所以这里也把大部分初始化韩式放在了这里
        {
            enableordisable_button_flag = OPENSERIAL_BUTTON | DOWNLOAD_BUTTON;
            choose_which_button_to_use(enableordisable_button_flag);
            /***********************comboBox 初始化************************************************/
            //string[] com_str_arry = SerialPort.GetPortNames();

            //for (int i = 0; i < com_str_arry.Length; i++)
            //{
            //    serialport_comboBox.Items.Add(com_str_arry[i]);
            //}
            //combox_of_serial();
            timer1 = new System.Threading.Timer(new System.Threading.TimerCallback(mytimer1), null, 0, 500);

            /***************************************serialport************************************************************/
            serialPort1.DataReceived += new SerialDataReceivedEventHandler(ComReceive);//串口接收中断
            Thread _ComRec = new Thread(new ThreadStart(ComRec)); //查询串口接收数据线程声明
            _ComRec.Start();//启动线程

            /******************************************timer****************************************************/
            //timer = new System.Threading.Timer(new System.Threading.TimerCallback(mytimer), null, 0, -1);

            /********************************************listview 加载表头******************************************************************/
            this.shuju_listView.Columns.Add("序列号", 70, HorizontalAlignment.Left);
            this.shuju_listView.Columns.Add("序号", 30, HorizontalAlignment.Left);
            this.shuju_listView.Columns.Add("日期", 90, HorizontalAlignment.Left);
            this.shuju_listView.Columns.Add("时间", 100, HorizontalAlignment.Left);
            this.shuju_listView.Columns.Add("地理坐标", 180, HorizontalAlignment.Left);
            //对chart进行初始化设定
            #region
            leftup_chart.ChartAreas[0].AxisY.LabelStyle.Format = "N2";
            leftup_chart.Titles.Add("时域波形");
            leftup_chart.ChartAreas[0].AxisX.Title = "采样点/个";
            leftup_chart.ChartAreas[0].AxisY.Title = "幅值/V";

            leftdown_chart.ChartAreas[0].AxisY.LabelStyle.Format = "N2";
            leftdown_chart.Titles.Add("磁信号");
            leftdown_chart.ChartAreas[0].AxisX.Title = "采样点/个";
            leftdown_chart.ChartAreas[0].AxisY.Title = "幅值/V";

            rightup_chart.ChartAreas[0].AxisY.LabelStyle.Format = "N2";
            rightup_chart.Titles.Add("22Hz幅值");
            rightup_chart.ChartAreas[0].AxisX.Title = "频域点/个";
            rightup_chart.ChartAreas[0].AxisY.Title = "幅值比/dB";

            rightdown_chart.ChartAreas[0].AxisY.LabelStyle.Format = "N2";
            rightdown_chart.Titles.Add("22Hz幅值之差");
            rightdown_chart.ChartAreas[0].AxisX.Title = "频域点/个";
            rightdown_chart.ChartAreas[0].AxisY.Title = "幅值比/dB";
            #endregion
        }

        /*****************************************serialport function*********************************************************/
        private void ComReceive(object sender, SerialDataReceivedEventArgs e)//接收数据 中断只标志有数据需要读取，读取操作在中断外进行
        {
            //if (WaitClose) return;//如果正在关闭串口，则直接返回
            //Thread.Sleep(10);//发送和接收均为文本时，接收中为加入判断是否为文字的算法，发送你（C4E3），接收可能识别为C4,E3，可用在这里加延时解决
            if (recStaus)//如果已经开启接收
            {
                byte[] recBuffer;//接收缓冲区
                try
                {
                    recBuffer = new byte[serialPort1.BytesToRead];//接收数据缓存大小
                    serialPort1.Read(recBuffer, 0, recBuffer.Length);//读取数据
                    if (recBuffer.Length != 0)
                    {
                        recQueue.Enqueue(recBuffer);//读取数据入列Enqueue（全局）
                    }
                }
                catch
                {
                    MessageBox.Show("无法接收数据，原因未知！");
                }

            }
            else//暂停接收
            {
                serialPort1.DiscardInBuffer();//清接收缓存
            }
        }
        int zongshu_rec = 0;
        List<byte> rec_Buffer_Global = new List<byte> { };///////////////////////////
        List<byte> Save_rec_Buffer_Global = new List<byte> { };
        List<List<byte>> shuju_listview_erwei = new List<List<byte>>();
        List<List<byte>> shuju_chart1_1_erwei = new List<List<byte>>();
        List<List<byte>> shuju_chart1_2_erwei = new List<List<byte>>();
        List<List<byte>> shuju_chart2_ci_erwei = new List<List<byte>>();
        List<List<byte>> shuju_chart3_1_erwei = new List<List<byte>>();
        List<List<byte>> shuju_chart3_2_erwei = new List<List<byte>>();
        List<List<byte>> shuju_chart4_erwei = new List<List<byte>>();

        bool readshuju_kaishi_flag = false;//以此flag消除按钮按下至下位机回复这段时间所产生的延时误判
        void ComRec()//接收线程，窗口初始化中就开始启动运行
        {
            int readshuju_jishu = 0;//“读取数据”按钮标志位，“读取数据”按钮按下且数据接收完成之后，以此标志位使listview显示新的数据
            bool readshuju_thread_flag = false;
            while (true)//一直查询串口接收线程中是否有新数据
            {
                if (recQueue.Count > 0)//当串口接收线程中有新的数据时候，队列中有新进的成员recQueue.Count > 0
                {
                    /********************************将数据全部存储在全局变量rec_Buffer_Global中*****************************************/
                    byte[] recBuffer = (byte[])recQueue.Dequeue();//出列Dequeue（全局）
                    if (recBuffer != null)
                    {
                        rec_Buffer_Global.AddRange(recBuffer);
                        /*************************************显示已接收个数，便于调试*********************************************/
                        zongshu_rec += recBuffer.Length;
                        //this.textBox1.Text = this.textBox1.Text + recBuffer16.ToString();此语句有缺陷，貌似每次运行都会将整个textBox控件刷新this.message_textBox.Text = ( (float)zongshu_rec / (float)(shangchuanzishu_int + 256) ).ToString("0.0%");
                        Action<string> actiondelegate = (x) => { this.serialport_rec_num_label.Text = x; };
                        this.serialport_rec_num_label.Invoke(actiondelegate, Convert.ToString(zongshu_rec));
                        if (readshuju_baifen == true)
                        {
                            //readshuju_baifen = false;

                            Action<string> actiondelegate2 = (x) => { this.message_textBox.Text = x; };
                            this.message_textBox.Invoke(actiondelegate2, ((float)zongshu_rec / (float)(shangchuanzishu_int + 256)).ToString("0%"));
                        }
                    }
                    readshuju_kaishi_flag = true;//只有数据开始接收了此标志位才会保持为真
                    readshuju_jishu = 0;//“读取数据”按钮所需标志位
                    i_timer = 0;


                    if (readshusju_button_flag == true)
                        stopWatch.Restart();
                }
                //判断语句有两部分组成，第一部分是判断此次接收的数据是由哪个按钮引发的，第二个是判断数据是否接收完成
                //因为已经知道接受的数据是由什么按钮引发的，而每个按钮所产生的回复指令的长度大体是一定的，所以可以判断是否接受完成
                else if (readxinxi_int == true && rec_Buffer_Global.Count >= 61)
                {
                    Action<int> actionButton = (x) => { choose_which_button_to_use(x); };
                    enableordisable_button_flag = enableordisable_button_flag_old | READSHUJU_BUTTON |
                                                                                    WRITEXULIEHAO_BUTTON | WRITECICHUFAYUZHI_BUTTON |
                                                                                    WRTIECHUFAYUZHI_BUTTON | WRITEGONGZUOMOSHI_BUTTON;
                    this.Invoke(actionButton, enableordisable_button_flag);
                    /********************************对读取信息按钮产生的回复信息进行处理现实*************************************************/
                    #region
                    readxinxi_int = false;//此标志位在相应按钮中会改变
                    byte[] trans_yingjianbanben = new byte[4];
                    byte[] trans_gujianbanben = new byte[4];
                    byte[] trans_dianchidianliang = new byte[4];
                    byte[] trans_xuliehao = new byte[12];
                    byte[] trans_chufayuzhi = new byte[4];
                    byte[] trans_cichufayuzhi = new byte[4];

                    for (int i = 0; i < 4; i++)
                    {
                        shangchuanzishu_int = shangchuanzishu_int * 256 + rec_Buffer_Global[i + 12];
                        chufacishu_int = chufacishu_int * 256 + rec_Buffer_Global[i + 16];
                        gongzuomoshi = gongzuomoshi * 256 + rec_Buffer_Global[i + 20];
                        trans_yingjianbanben[i] = rec_Buffer_Global[i + 32];
                        trans_gujianbanben[i] = rec_Buffer_Global[i + 36];
                        trans_dianchidianliang[3 - i] = rec_Buffer_Global[i + 40];
                        trans_chufayuzhi[3 - i] = rec_Buffer_Global[i + 24];
                        trans_cichufayuzhi[3 - i] = rec_Buffer_Global[i + 56];
                    }
                    for (int i = 0; i < 12; i++)
                    {
                        trans_xuliehao[i] = rec_Buffer_Global[i + 44];
                    }
                    shengyukongjian_dbl = (float)1 - (float)shangchuanzishu_int / (float)8384512;//剩余空间百分数
                    shangchuanzishu_int -= 4096;//对shangchuanzishu_int数据按照通信协议进行调整

                    yingjianbanben_str = System.Text.Encoding.ASCII.GetString(trans_yingjianbanben);
                    gujianbanben_str = System.Text.Encoding.ASCII.GetString(trans_gujianbanben);
                    dianchidianliang_dbl = BitConverter.ToSingle(trans_dianchidianliang, 0);
                    xuliehao_str = System.Text.Encoding.ASCII.GetString(trans_xuliehao);
                    chufayuzhi_dbl = BitConverter.ToSingle(trans_chufayuzhi, 0);
                    cichufayuzhi_dbl = BitConverter.ToSingle(trans_cichufayuzhi, 0);

                    //电池电量
                    if (dianchidianliang_dbl >= 3)
                        dianchidianliang_dbl = 1;
                    else if (dianchidianliang_dbl <= 2.5)
                        dianchidianliang_dbl = 0;
                    else
                        dianchidianliang_dbl = (dianchidianliang_dbl - (float)2.5) * 2;

                    Action<string> actiondelegate = (x) => { this.message_textBox.Text += (x + "\r\n"); };
                    this.message_textBox.Invoke(actiondelegate, "剩余空间  ：" + shengyukongjian_dbl.ToString("0.0%"));//error
                    this.message_textBox.Invoke(actiondelegate, "上传字数  ：" + shangchuanzishu_int.ToString());
                    this.message_textBox.Invoke(actiondelegate, "触发次数  ：" + chufacishu_int.ToString());
                    this.message_textBox.Invoke(actiondelegate, "工作模式  ：" + gongzuomoshi.ToString());
                    this.message_textBox.Invoke(actiondelegate, "硬件版本号：" + yingjianbanben_str);

                    this.message_textBox.Invoke(actiondelegate, "固件版本号：" + gujianbanben_str);
                    this.message_textBox.Invoke(actiondelegate, "电池电量  ：" + dianchidianliang_dbl.ToString("0.0%"));//error
                    this.message_textBox.Invoke(actiondelegate, "序列号    ：" + xuliehao_str.Substring(3));
                    this.message_textBox.Invoke(actiondelegate, "触发阈值  ：" + chufayuzhi_dbl.ToString("0.0"));
                    this.message_textBox.Invoke(actiondelegate, "磁触发阈值：" + cichufayuzhi_dbl.ToString("0.0"));

                    rec_Buffer_Global.Clear();
                    #endregion
                }
                //这里判断数据是否接收完成的方法是基于“读取信息”按钮所产生的回复信息中的数据总量和实际数据上传总量是差256或者512
                //else if (readshusju_button_flag == true && (zongshu_rec == (shangchuanzishu_int + 256) || zongshu_rec == (shangchuanzishu_int + 512)))
                else if (readshuju_thread_flag == true)
                {
                    #region
                    readshuju_thread_flag = false;

                    //readshusju_button_flag = false;//只有将数据进行处理之后才允许将此标志位置零，否则循环等待直至readshuju_jishu超过1000000
                    //readshuju_jishu = 0;
                    readshuju_baifen = false;//只有读在读数据的时候才会有百分数的显示，此flag置零说明之后的数据接收不会以百分数的形式显示出来

                    List<byte> temp_rec_Buffer_Global = new List<byte>(rec_Buffer_Global);

                    //if (chufacishu_int == 0)
                    //{
                    //    Action<string> actiondelegate = (x) => { this.message_textBox.Text = x; };
                    //    this.serialport_rec_num_label.Invoke(actiondelegate, "没有数据");
                    //}

                    /*******************************************************将数据进行分配***************************************************************************/
                    rem_shuju_return_int = shujufenpei(rec_Buffer_Global, shuju_listview_erwei, shuju_chart1_1_erwei, shuju_chart1_2_erwei, shuju_chart2_ci_erwei, shuju_chart3_1_erwei, shuju_chart3_2_erwei, rec_Buffer_Global.Count);

                    /****************************************listview*********************************************************/
                    #region
                    Action<string> actionlistviewbegin = (x) => { this.shuju_listView.BeginUpdate(); this.shuju_listView.Items.Clear(); };
                    this.shuju_listView.Invoke(actionlistviewbegin, "Over");

                    display_shuju_listview(rem_shuju_return_int);
                    //this.shuju_listView.EndUpdate();  //结束数据处理，UI界面一次性绘制。
                    Action<string> actionlistviewend = (x) => { this.shuju_listView.EndUpdate(); };
                    this.shuju_listView.Invoke(actionlistviewend, "Over");
                    #endregion

                    if (message_textBox.Text == "100%" || shuju_jieshouwanquan_or_not(rem_shuju_return_int, chufacishu_int, rec_Buffer_Global) == 0)//////////////////////
                    {
                        Action<string> actiondelegate = (x) => { this.message_textBox.Text = x; };
                        this.serialport_rec_num_label.Invoke(actiondelegate, "数据接收完成");
                        //rec_Buffer_Global.CopyTo(Save_rec_Buffer_Global, 0);
                        for (int i = 0; i < rec_Buffer_Global.Count; i++)
                        {
                            //Save_rec_Buffer_Global[i] = rec_Buffer_Global[i];
                            Save_rec_Buffer_Global.Add(rec_Buffer_Global[i]);
                        }
                    }
                    else if (shuju_jieshouwanquan_or_not(rem_shuju_return_int, chufacishu_int, rec_Buffer_Global) == 1)
                    {
                        Action<string> actiondelegate = (x) => { this.message_textBox.Text = x; };
                        this.serialport_rec_num_label.Invoke(actiondelegate, "数据接收中断");
                    }
                    else
                    {
                        Action<string> actiondelegate = (x) => { this.message_textBox.Text = x; };
                        this.serialport_rec_num_label.Invoke(actiondelegate, "没有数据");
                    }

                    //Action<string> actiondelegate = (x) => { this.message_textBox.Text = x; };
                    //this.serialport_rec_num_label.Invoke(actiondelegate, "数据接收完成");
                    zongshu_rec = 0;
                    #endregion
                    Action<int> actionButton = (x) => { choose_which_button_to_use(x); };
                    enableordisable_button_flag = enableordisable_button_flag_old | SAVESHUJU_BUTTON | JIEXISHUJU_BUTTON |
                                                    SHENGCHENGBAOBIAO_BUTTON | SAVEIMAGE_BUTTON;
                    this.Invoke(actionButton, enableordisable_button_flag);

                    //timer.Change(0, -1);
                }

                else if (writexuliehao_bool == true && rec_Buffer_Global.Count >= 9)
                {//“写序列号” “写触发阈值” “写工作模式” “写磁触发模式”按钮中的代码大致相同，这里只对“写序列号”按钮进行解释
                    #region
                    writexuliehao_bool = false;//此标志位在相应按钮中会改变
                    rec_Buffer_Global.Clear();//时常对rec_Buffer_Global进行清除，保持革命队伍的纯洁性

                    Action<string> actiondelegate = (x) => { this.message_textBox.Text = x; };
                    this.serialport_rec_num_label.Invoke(actiondelegate, "写序列号完成");//告诉用户相应数据的更改已经完成
                    zongshu_rec = 0;//接收数据数量清零
                    #endregion
                    Action<int> actionButton = (x) => { choose_which_button_to_use(x); };
                    enableordisable_button_flag = enableordisable_button_flag_old;
                    this.Invoke(actionButton, enableordisable_button_flag);
                }
                else if (writechufayuzhi_bool == true && rec_Buffer_Global.Count >= 9)
                {
                    #region
                    writechufayuzhi_bool = false;
                    rec_Buffer_Global.Clear();

                    Action<string> actiondelegate = (x) => { this.message_textBox.Text = x; };
                    this.serialport_rec_num_label.Invoke(actiondelegate, "写触发阈值完成");
                    zongshu_rec = 0;
                    #endregion
                    Action<int> actionButton = (x) => { choose_which_button_to_use(x); };
                    enableordisable_button_flag = enableordisable_button_flag_old;
                    this.Invoke(actionButton, enableordisable_button_flag);
                }
                else if (writegongzuomoshi_bool == true && rec_Buffer_Global.Count >= 9)
                {
                    #region
                    writegongzuomoshi_bool = false;
                    rec_Buffer_Global.Clear();

                    Action<string> actiondelegate = (x) => { this.message_textBox.Text = x; };
                    this.serialport_rec_num_label.Invoke(actiondelegate, "写工作模式完成");
                    zongshu_rec = 0;
                    #endregion
                    Action<int> actionButton = (x) => { choose_which_button_to_use(x); };
                    enableordisable_button_flag = enableordisable_button_flag_old;
                    this.Invoke(actionButton, enableordisable_button_flag);
                }
                else if (writecichufayuzhi_bool == true && rec_Buffer_Global.Count >= 9)
                {
                    #region
                    writecichufayuzhi_bool = false;
                    rec_Buffer_Global.Clear();

                    Action<string> actiondelegate = (x) => { this.message_textBox.Text = x; };
                    this.serialport_rec_num_label.Invoke(actiondelegate, "写触发阈值完成");
                    zongshu_rec = 0;
                    #endregion
                    Action<int> actionButton = (x) => { choose_which_button_to_use(x); };
                    enableordisable_button_flag = enableordisable_button_flag_old;
                    this.Invoke(actionButton, enableordisable_button_flag);
                }
                else if (clearflash_button_bool == true && rec_Buffer_Global.Count >= 9)
                {
                    #region
                    clearflash_button_bool = false;
                    rec_Buffer_Global.Clear();

                    Action<string> actiondelegate = (x) => { this.message_textBox.Text = x; };
                    this.serialport_rec_num_label.Invoke(actiondelegate, "清除flash完成");
                    zongshu_rec = 0;
                    #endregion
                    Action<int> actionButton = (x) => { choose_which_button_to_use(x); };
                    enableordisable_button_flag = enableordisable_button_flag_old;
                    this.Invoke(actionButton, enableordisable_button_flag);
                }
                //else if (readshusju_button_flag == true && readshuju_kaishi_flag == true)//如果没有数据接收， readshuju_kaishi_flag是false,也就不会进入到程序段中了
                //{//“读取数据”按钮所产生的回复信息接收完成之后，如果没有新的指令发出，就不会有新的数据接收。利用数据接收完成之后不会有新的数据出现这一特点，判定此次数据是否接受完成
                //    readshuju_jishu++;///////////////////////////////test_15_01
                //    if (readshuju_jishu >= 10000000)
                //    {//
                //        readshuju_jishu = 0;
                //        readshuju_thread_flag = true;
                //        readshusju_button_flag = false;
                //    }
                //}
                else if (readshusju_button_flag == true)//如果没有数据接收， readshuju_kaishi_flag是false,也就不会进入到程序段中了
                {
                    //if (i_timer > 10)
                    //{
                    //    i_timer = 0;
                    //    readshuju_thread_flag = true;
                    //    readshusju_button_flag = false;
                    //}
                    if (stopWatch.ElapsedMilliseconds == 3000)
                    {
                        readshuju_thread_flag = true;
                        readshusju_button_flag = false;
                        stopWatch.Reset();
                    }
                }
            }

        }


        /*********************************************button*****************************************************************/
        bool openorcloseserial_button_flag = false;
        private void openserial_button_Click(object sender, EventArgs e)
        {
            #region
            if (openorcloseserial_button_flag == false)
            {
                openorcloseserial_button_flag = true;
                try
                {
                    serialPort1.PortName = serialport_comboBox.Text;
                    serialPort1.Open();//readbuffer不能是65535，否则出错
                    this.message_textBox.Text = "成功打开串口";
                    openserial_button.Text = "关闭串口";

                    enableordisable_button_flag |= (READXINXI_BUTTON | CLEARFLASH_BUTTON);
                    choose_which_button_to_use(enableordisable_button_flag);
                }
                catch
                {
                    MessageBox.Show("error", "error");
                }
            }
            else
            {
                openorcloseserial_button_flag = false;
                try
                {
                    serialPort1.Close();//readbuffer不能是65535，否则出错
                    this.message_textBox.Text = "成功关闭串口";
                    openserial_button.Text = "打开串口";

                    enableordisable_button_flag_old = enableordisable_button_flag;
                    enableordisable_button_flag &= ~(READXINXI_BUTTON | READSHUJU_BUTTON | CLEARFLASH_BUTTON |
                                                    WRITEXULIEHAO_BUTTON | WRITECICHUFAYUZHI_BUTTON |
                                                    WRTIECHUFAYUZHI_BUTTON | WRITEGONGZUOMOSHI_BUTTON);
                    choose_which_button_to_use(enableordisable_button_flag);
                }
                catch
                {
                    MessageBox.Show("error", "error");
                }
            }
            #endregion
        }

        private void clearserial_button_Click(object sender, EventArgs e)
        {
            leftup_chart.Series.Clear();
            rightup_chart.Series.Clear();
            leftdown_chart.Series.Clear();
            rightdown_chart.Series.Clear();

            this.shuju_listView.Clear();
            this.shuju_listView.Columns.Add("序列号", 70, HorizontalAlignment.Left);
            this.shuju_listView.Columns.Add("序号", 30, HorizontalAlignment.Left);
            this.shuju_listView.Columns.Add("日期", 90, HorizontalAlignment.Left);
            this.shuju_listView.Columns.Add("时间", 100, HorizontalAlignment.Left);
            this.shuju_listView.Columns.Add("地理坐标", 180, HorizontalAlignment.Left);

            List<List<byte>> shuju_chart1_1_erwei = new List<List<byte>>();
            List<List<byte>> shuju_chart1_2_erwei = new List<List<byte>>();
            List<List<byte>> shuju_chart2_ci_erwei = new List<List<byte>>();
            List<List<byte>> shuju_chart3_1_erwei = new List<List<byte>>();
            List<List<byte>> shuju_chart3_2_erwei = new List<List<byte>>();
            List<List<byte>> shuju_chart4_erwei = new List<List<byte>>();

            recQueue.Clear();
        }

        bool readxinxi_int = false;//任何会会使下位机上传数据的按钮都会有此标志位，每次数据接收完成，在接收线程中会根据这些标志位判断出用户在下位机上传数据之前是要执行什么功能。
        private void readxinxi_button_Click(object sender, EventArgs e)
        {
            enableordisable_button_flag_old = enableordisable_button_flag;
            enableordisable_button_flag = ALLBUTTON_DISABLE;
            choose_which_button_to_use(enableordisable_button_flag);
            #region
            rec_Buffer_Global.Clear();
            zongshu_rec = 0;
            this.message_textBox.Text = "";

            byte[] write_Buffer = { 0xFE, 0xF5, 0xFE, 0xF5, 0x00, 0x01, 0x00, 0x00, 0x01, 0xFE, 0xF5, 0xFE, 0xF5, 0x00, 0x01, 0x00, 0x00, 0x01 };
            serialPort1.Write(write_Buffer, 0, 18);
            readxinxi_int = true;
            #endregion
        }

        bool readshuju_baifen = false;
        bool readshusju_button_flag = false;
        Stopwatch stopWatch = new Stopwatch();
        private void readshuju_button_Click(object sender, EventArgs e)
        {
            clearserial_button_Click(this, null);

            if (chufacishu_int == 0 || shangchuanzishu_int == 0)
            {
                this.message_textBox.Text = "没有数据";
                return;
            }


            enableordisable_button_flag_old = enableordisable_button_flag;
            enableordisable_button_flag = ALLBUTTON_DISABLE;
            choose_which_button_to_use(enableordisable_button_flag);
            #region
            readshuju_kaishi_flag = false;//以此标志位消除按键按下至数据回复所产生的误判

            rec_Buffer_Global.Clear();
            shuju_listview_erwei.Clear();
            shuju_chart1_1_erwei.Clear();
            shuju_chart1_2_erwei.Clear();
            shuju_chart2_ci_erwei.Clear();
            shuju_chart3_1_erwei.Clear();
            shuju_chart3_2_erwei.Clear();

            zongshu_rec = 0;
            byte[] write_Buffer = { 0xFE, 0xF5, 0xFE, 0xF5, 0x00, 0x02, 0x00, 0x00, 0x02, 0xFE, 0xF5, 0xFE, 0xF5, 0x00, 0x02, 0x00, 0x00, 0x02 };
            serialPort1.Write(write_Buffer, 0, 18);

            readshuju_baifen = true;//此后接收数据的时候会出现百分数的表示方法
            readshusju_button_flag = true;
            #endregion

            i_timer = 0;
            //timer = new System.Threading.Timer(new System.Threading.TimerCallback(mytimer), null, 100, 1);
            //timer.Change(500, 20);
        }
        int i_timer = 0;
        //System.Threading.Timer timer = null;
        public void mytimer(object a)
        {
            i_timer++;
            //this.label1.Text = i_timer.ToString();

            //Action<int> actionlistviewbegin = (x) => { this.label1.Text = x.ToString(); };
            //this.label1.Invoke(actionlistviewbegin, i_timer);
        }

        public void mytimer1(object a)
        {
            combox_of_serial();
        }

        private void saveshuju_button_Click(object sender, EventArgs e)
        {
            enableordisable_button_flag_old = enableordisable_button_flag;
            enableordisable_button_flag = ALLBUTTON_DISABLE;
            choose_which_button_to_use(enableordisable_button_flag);
            #region
            System.Windows.Forms.SaveFileDialog sfd = new SaveFileDialog();//注意 这里是SaveFileDialog,不是OpenFileDialog
            sfd.DefaultExt = "txt";
            sfd.Filter = "文本文件(*.txt)|*.txt";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                message_textBox.Text = "正在保存数据，请等待。。。。";

                StringBuilder recBuffer16 = new StringBuilder();//定义16进制接收缓存
                foreach (byte rec in Save_rec_Buffer_Global)
                {
                    recBuffer16.AppendFormat("{0:X2}" + " ", rec);//X2表示十六进制格式（大写），域宽2位，不足的左边填0。
                }

                string fileName = sfd.FileName;//std.FileName表示对话框中的路径名称
                FileStream fs = null;
                try
                {
                    File.Delete(fileName);//有重名的就删除掉
                    fs = new FileStream(fileName, FileMode.OpenOrCreate);//返回文件表示符

                    using (StreamWriter writer = new StreamWriter(fs))
                    {
                        writer.Write(recBuffer16);
                    }
                }
                finally
                {
                    if (fs != null)
                        fs.Dispose();
                    message_textBox.Text = "保存数据完成";//保存完成
                }
            }
            #endregion
            enableordisable_button_flag = enableordisable_button_flag_old;
            choose_which_button_to_use(enableordisable_button_flag);
        }

        private void jiexishuju_button_Click(object sender, EventArgs e)
        {

        }

        private void shengchengbaobiao_button_Click(object sender, EventArgs e)
        {
            enableordisable_button_flag_old = enableordisable_button_flag;
            enableordisable_button_flag = ALLBUTTON_DISABLE;
            choose_which_button_to_use(enableordisable_button_flag);
            message_textBox.Text = " ";
            #region
            System.Windows.Forms.SaveFileDialog sfd = new SaveFileDialog();//注意 这里是SaveFileDialog,不是OpenFileDialog
            sfd.DefaultExt = "xls";
            sfd.Filter = "文件(*.xls)|*.xls";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                message_textBox.Text = "正在生成报表，请等待。。。。";
                DoExport(shuju_listView, sfd.FileName);//网上的例程
                message_textBox.Text = "生成报表完成";
            }
            #endregion
            enableordisable_button_flag = enableordisable_button_flag_old;
            choose_which_button_to_use(enableordisable_button_flag);
        }

        private void saveimage_button_Click(object sender, EventArgs e)
        {
            enableordisable_button_flag_old = enableordisable_button_flag;
            enableordisable_button_flag = ALLBUTTON_DISABLE;
            choose_which_button_to_use(enableordisable_button_flag);
            message_textBox.Text = " ";
            #region
            System.Windows.Forms.SaveFileDialog sfd = new SaveFileDialog();//注意 这里是SaveFileDialog,不是OpenFileDialog
            sfd.DefaultExt = "jpeg";
            sfd.Filter = "图片(*.jpeg)|*.jpeg";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                message_textBox.Text = "正在保存图像，请等待。。。。";

                string[] temp_str = sfd.FileName.Split('.');//以‘。’为分割符，分成两份，取前一份
                //总共四个图片，要保存四份，添加了一定的命名规律
                leftup_chart.SaveImage(temp_str[0] + "_Image1.jpeg", System.Drawing.Imaging.ImageFormat.Jpeg);
                rightup_chart.SaveImage(temp_str[0] + "_Image2.jpeg", System.Drawing.Imaging.ImageFormat.Jpeg);
                leftdown_chart.SaveImage(temp_str[0] + "_Image3.jpeg", System.Drawing.Imaging.ImageFormat.Jpeg);
                rightdown_chart.SaveImage(temp_str[0] + "_Image4.jpeg", System.Drawing.Imaging.ImageFormat.Jpeg);

                message_textBox.Text = "保存图像完成";
            }
            #endregion

            enableordisable_button_flag = enableordisable_button_flag_old;
            choose_which_button_to_use(enableordisable_button_flag);
        }

        bool clearflash_button_bool = false;
        private void clearflash_button_Click(object sender, EventArgs e)
        {
            enableordisable_button_flag_old = enableordisable_button_flag;
            enableordisable_button_flag = ALLBUTTON_DISABLE;
            choose_which_button_to_use(enableordisable_button_flag);
            this.message_textBox.Text = "正在清除flash";
            #region
            rec_Buffer_Global.Clear();
            zongshu_rec = 0;
            //this.message_textBox.Text = "";

            byte[] write_Buffer = { 0xFE, 0xF5, 0xFE, 0xF5, 0x00, 0x03, 0x00, 0x00, 0x03, 0xFE, 0xF5, 0xFE, 0xF5, 0x00, 0x03, 0x00, 0x00, 0x03 };
            serialPort1.Write(write_Buffer, 0, 18);
            clearflash_button_bool = true;
            #endregion
        }

        bool writexuliehao_bool = false;
        private void writexuliehao_button_Click(object sender, EventArgs e)
        {
            enableordisable_button_flag_old = enableordisable_button_flag;
            enableordisable_button_flag = ALLBUTTON_DISABLE;
            choose_which_button_to_use(enableordisable_button_flag);
            #region
            //if (yingjianbanbenhao_textBox.Text == "" || gujianbanbenhao_textBox.Text == "" || xuliehao_textBox.Text == "")
            //    return;//如果对话框中没有数据，则退出

            if (yingjianbanbenhao_textBox.Text.Length != 4 || gujianbanbenhao_textBox.Text.Length != 4 || xuliehao_textBox.Text.Length != 9)
            {
                enableordisable_button_flag = enableordisable_button_flag_old;
                choose_which_button_to_use(enableordisable_button_flag);
                return;//如果对话框中没有数据，则退出
            }
            rec_Buffer_Global.Clear();
            zongshu_rec = 0;
            this.message_textBox.Text = "";

            byte[] write_Buffer = new byte[33];
            List<byte> write_Buffer_list = new List<byte> { 0xFE, 0xF5, 0xFE, 0xF5, 0x00, 0x07, 0x00, 0x18, 0x00, 0x01, 0x00, 0x01 };
            write_Buffer_list.AddRange(System.Text.Encoding.ASCII.GetBytes(yingjianbanbenhao_textBox.Text));
            write_Buffer_list.AddRange(System.Text.Encoding.ASCII.GetBytes(gujianbanbenhao_textBox.Text));
            write_Buffer_list.AddRange(new byte[] { 0x20, 0x20, 0x20 });
            write_Buffer_list.AddRange(System.Text.Encoding.ASCII.GetBytes(xuliehao_textBox.Text));

            int sum = 0;
            for (int i = 4; i < 32; i++)
            {
                sum += write_Buffer_list[i];
            }
            sum = sum & 0xff;
            write_Buffer_list.Add((byte)sum);

            if (write_Buffer_list.Count != 33)//如果数据不足，说明输入错误
                return;
            for (int i = 0; i < 33; i++)
            {
                write_Buffer[i] = write_Buffer_list[i];
            }
            serialPort1.Write(write_Buffer, 0, 33);
            serialPort1.Write(write_Buffer, 0, 33);
            writexuliehao_bool = true;
            #endregion
        }

        bool writechufayuzhi_bool = false;
        private void writechufayuzhi_button_Click(object sender, EventArgs e)
        {
            enableordisable_button_flag_old = enableordisable_button_flag;
            enableordisable_button_flag = ALLBUTTON_DISABLE;
            choose_which_button_to_use(enableordisable_button_flag);
            #region
            if (shuipingyuzhi_textBox.Text == "" || liangtongdaochazhi_textBox.Text == "")
            {
                enableordisable_button_flag = enableordisable_button_flag_old;
                choose_which_button_to_use(enableordisable_button_flag);
                return;//如果对话框中没有数据，则退出
            }
            rec_Buffer_Global.Clear();
            zongshu_rec = 0;
            this.message_textBox.Text = "";

            byte[] write_Buffer = new byte[21];
            List<byte> write_Buffer_list = new List<byte> { 0xFE, 0xF5, 0xFE, 0xF5, 0x00, 0x04, 0x00, 0x0c, 0x00, 0x01, 0x00, 0x01 };
            List<byte> temp_list = new List<byte>(BitConverter.GetBytes(Convert.ToSingle(shuipingyuzhi_textBox.Text)));
            temp_list.Reverse();
            write_Buffer_list.AddRange(temp_list);

            temp_list = new List<byte>(BitConverter.GetBytes(Convert.ToSingle(liangtongdaochazhi_textBox.Text)));
            temp_list.Reverse();
            write_Buffer_list.AddRange(temp_list);

            int sum = 0;
            for (int i = 4; i < 20; i++)
            {
                sum += write_Buffer_list[i];
            }
            sum = sum & 0xff;
            write_Buffer_list.Add((byte)sum);

            if (write_Buffer_list.Count != 21)//如果数据不足，说明输入错误
                return;
            for (int i = 0; i < 21; i++)
            {
                write_Buffer[i] = write_Buffer_list[i];
            }
            serialPort1.Write(write_Buffer, 0, 21);
            serialPort1.Write(write_Buffer, 0, 21);
            writechufayuzhi_bool = true;
            #endregion
        }

        bool writegongzuomoshi_bool = false;
        private void writegongzuomoshi_button_Click(object sender, EventArgs e)
        {
            enableordisable_button_flag_old = enableordisable_button_flag;
            enableordisable_button_flag = ALLBUTTON_DISABLE;
            choose_which_button_to_use(enableordisable_button_flag);
            #region
            if (gongzuomoshi_textBox.Text == "")
            {
                enableordisable_button_flag = enableordisable_button_flag_old;
                choose_which_button_to_use(enableordisable_button_flag);
                return;//如果对话框中没有数据，则退出
            }
            rec_Buffer_Global.Clear();
            zongshu_rec = 0;
            this.message_textBox.Text = "";

            byte[] write_Buffer = new byte[17];
            List<byte> write_Buffer_list = new List<byte> { 0xFE, 0xF5, 0xFE, 0xF5, 0x00, 0x05, 0x00, 0x08, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00 };
            List<byte> temp_list = new List<byte>(BitConverter.GetBytes(Convert.ToByte(gongzuomoshi_textBox.Text)));
            temp_list.Reverse();
            write_Buffer_list.AddRange(temp_list);

            int sum = 0;
            for (int i = 4; i < 16; i++)
            {
                sum += write_Buffer_list[i];
            }
            sum = sum & 0xff;
            write_Buffer_list.Add((byte)sum);

            if (write_Buffer_list.Count != 17)//如果数据不足，说明输入错误
                return;
            for (int i = 0; i < 17; i++)
            {
                write_Buffer[i] = write_Buffer_list[i];
            }
            serialPort1.Write(write_Buffer, 0, 17);
            serialPort1.Write(write_Buffer, 0, 17);
            writegongzuomoshi_bool = true;
            #endregion
        }

        bool writecichufayuzhi_bool = false;
        private void writecichufayuzhi_button_Click(object sender, EventArgs e)
        {
            enableordisable_button_flag_old = enableordisable_button_flag;
            enableordisable_button_flag = ALLBUTTON_DISABLE;
            choose_which_button_to_use(enableordisable_button_flag);
            #region
            if (cichufayuzhi_textBox.Text == "")
            {
                enableordisable_button_flag = enableordisable_button_flag_old;
                choose_which_button_to_use(enableordisable_button_flag);
                return;//如果对话框中没有数据，则退出
            }
            rec_Buffer_Global.Clear();
            zongshu_rec = 0;
            this.message_textBox.Text = "";

            byte[] write_Buffer = new byte[17];
            List<byte> write_Buffer_list = new List<byte> { 0xFE, 0xF5, 0xFE, 0xF5, 0x00, 0x09, 0x00, 0x08, 0x00, 0x01, 0x00, 0x01 };
            List<byte> temp_list = new List<byte>(BitConverter.GetBytes(Convert.ToSingle(cichufayuzhi_textBox.Text)));
            temp_list.Reverse();
            write_Buffer_list.AddRange(temp_list);

            int sum = 0;
            for (int i = 4; i < 16; i++)
            {
                sum += write_Buffer_list[i];
            }
            sum = sum & 0xff;
            write_Buffer_list.Add((byte)sum);

            if (write_Buffer_list.Count != 17)//如果数据不足，说明输入错误
                return;
            for (int i = 0; i < 17; i++)
            {
                write_Buffer[i] = write_Buffer_list[i];
            }
            serialPort1.Write(write_Buffer, 0, 17);
            serialPort1.Write(write_Buffer, 0, 17);
            writecichufayuzhi_bool = true;
            #endregion
        }

        int index_Global = 0;
        bool flag_pandushujushifouyouxiao = false;

        private void shuju_listView_MouseClick(object sender, MouseEventArgs e)
        {
            index_Global = this.shuju_listView.SelectedItems[0].Index;//不知道为什么要是0，但事实证明0是好使的

            timeDomain_chart_display(leftup_chart, index_Global, shuju_chart1_1_erwei, shuju_chart1_2_erwei);
            /**************************************************************************************/

            if (Hz_fuZhi_chart_display(rightup_chart, index_Global, shuju_chart3_1_erwei, shuju_chart3_2_erwei) != 0)
                return;
            /*******************************************************************************/

            if (cixinhao_chart_display(leftdown_chart, index_Global, shuju_chart2_ci_erwei, null) != 0)
                return;
            /**************************************************************************************/

            Hz_fuZhiCha_chart_display(rightdown_chart, index_Global, shuju_chart3_1_erwei, shuju_chart3_2_erwei);//因为数据之前用过，所以不对数据的有效性进行检测

            flag_pandushujushifouyouxiao = true;//本次解析有效，可以呼叫子窗口

            //clearserial_button_Click(this, null);
        }

        private void shuju_listView_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == 38)
            {
                index_Global--;

                if (index_Global < 0)
                {
                    index_Global = 0;
                }

                timeDomain_chart_display(leftup_chart, index_Global, shuju_chart1_1_erwei, shuju_chart1_2_erwei);
                /**************************************************************************************/

                if (Hz_fuZhi_chart_display(rightup_chart, index_Global, shuju_chart3_1_erwei, shuju_chart3_2_erwei) != 0)
                    return;
                /*******************************************************************************/

                if (cixinhao_chart_display(leftdown_chart, index_Global, shuju_chart2_ci_erwei, null) != 0)
                    return;
                /**************************************************************************************/

                Hz_fuZhiCha_chart_display(rightdown_chart, index_Global, shuju_chart3_1_erwei, shuju_chart3_2_erwei);//因为数据之前用过，所以不对数据的有效性进行检测

                flag_pandushujushifouyouxiao = true;//本次解析有效，可以呼叫子窗口
            }
            else if (e.KeyValue == 40)
            {
                index_Global++;

                if (index_Global > (rem_shuju_return_int - 1))
                {
                    index_Global = rem_shuju_return_int - 1;
                }

                timeDomain_chart_display(leftup_chart, index_Global, shuju_chart1_1_erwei, shuju_chart1_2_erwei);
                /**************************************************************************************/

                if (Hz_fuZhi_chart_display(rightup_chart, index_Global, shuju_chart3_1_erwei, shuju_chart3_2_erwei) != 0)
                    return;
                /*******************************************************************************/

                if (cixinhao_chart_display(leftdown_chart, index_Global, shuju_chart2_ci_erwei, null) != 0)
                    return;
                /**************************************************************************************/

                Hz_fuZhiCha_chart_display(rightdown_chart, index_Global, shuju_chart3_1_erwei, shuju_chart3_2_erwei);//因为数据之前用过，所以不对数据的有效性进行检测

                flag_pandushujushifouyouxiao = true;//本次解析有效，可以呼叫子窗口
            }
        }

        private void shuju_listView_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            //index_Global = this.shuju_listView.SelectedItems[0].Index;//不知道为什么要是0，但事实证明0是好使的

            //timeDomain_chart_display(leftup_chart, index_Global, shuju_chart1_1_erwei, shuju_chart1_2_erwei);
            ///**************************************************************************************/

            //if (Hz_fuZhi_chart_display(rightup_chart, index_Global, shuju_chart3_1_erwei, shuju_chart3_2_erwei) != 0)
            //    return;
            ///*******************************************************************************/

            //if (cixinhao_chart_display(leftdown_chart, index_Global, shuju_chart2_ci_erwei, null) != 0)
            //    return;
            ///**************************************************************************************/

            //Hz_fuZhiCha_chart_display(rightdown_chart, index_Global, shuju_chart3_1_erwei, shuju_chart3_2_erwei);//因为数据之前用过，所以不对数据的有效性进行检测

            //flag_pandushujushifouyouxiao = true;//本次解析有效，可以呼叫子窗口
        }


        int rem_shuju_return_int = 0;
        private void download_button_Click(object sender, System.EventArgs e)
        {
            #region
            rec_Buffer_Global.Clear();
            shuju_listview_erwei.Clear();
            shuju_chart1_1_erwei.Clear();
            shuju_chart1_2_erwei.Clear();
            shuju_chart2_ci_erwei.Clear();
            shuju_chart3_1_erwei.Clear();
            shuju_chart3_2_erwei.Clear();

            System.Windows.Forms.OpenFileDialog sfd = new OpenFileDialog();//注意 这里是OpenFileDialog,不是SaveFileDialog
            sfd.DefaultExt = "txt";
            sfd.Filter = "文本文件(*.txt)|*.txt";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                //DoExport(this.listView1, sfd.FileName);
                StreamReader rd = File.OpenText(sfd.FileName);
                string s = rd.ReadLine();
                string[] ss = s.Split(' ');
                //byte[] bytearray = new byte[ss.Length];
                byte temp_byte = 0;
                rec_Buffer_Global.Clear();
                for (int i = 0; i < ss.Length - 1; i++)
                {
                    temp_byte = Convert.ToByte(ss[i], 16);
                    rec_Buffer_Global.Add(temp_byte);
                }
                List<byte> temp_rec_Buffer_Global = new List<byte>(rec_Buffer_Global);

                /*******************************************************将数据进行分配***************************************************************************/
                rem_shuju_return_int = shujufenpei(rec_Buffer_Global, shuju_listview_erwei, shuju_chart1_1_erwei, shuju_chart1_2_erwei, shuju_chart2_ci_erwei, shuju_chart3_1_erwei, shuju_chart3_2_erwei, rec_Buffer_Global.Count);
                /****************************************listview*********************************************************/
                #region
                this.shuju_listView.BeginUpdate();   //数据更新，UI暂时挂起，直到EndUpdate绘制控件，可以有效避免闪烁并大大提高加载速度
                this.shuju_listView.Items.Clear();
                //Action<string> actionlistviewbegin = (x) => { this.shuju_listView.BeginUpdate(); };
                //this.shuju_listView.Invoke(actionlistviewbegin, "Over");

                display_shuju_listview(rem_shuju_return_int);
                this.shuju_listView.EndUpdate();  //结束数据处理，UI界面一次性绘制。
                //Action<string> actionlistviewend = (x) => { this.shuju_listView.EndUpdate(); };
                //this.shuju_listView.Invoke(actionlistviewend, "Over");
                #endregion

            }
            #endregion
            enableordisable_button_flag |= (SAVESHUJU_BUTTON | SAVEIMAGE_BUTTON | SHENGCHENGBAOBIAO_BUTTON);
            choose_which_button_to_use(enableordisable_button_flag);
        }


        /*****************************excel**********************************************/
        private void DoExport(ListView listView, string strFileName)
        {

            int rowNum = listView.Items.Count;

            int columnNum = listView.Items[0].SubItems.Count;

            int rowIndex = 1;

            int columnIndex = 0;

            if (rowNum == 0 || string.IsNullOrEmpty(strFileName))
            {

                return;

            }

            if (rowNum > 0)
            {



                Microsoft.Office.Interop.Excel.Application xlApp = new Excel.Application();

                if (xlApp == null)
                {

                    MessageBox.Show("无法创建excel对象，可能您的系统没有安装excel");

                    return;

                }

                xlApp.DefaultFilePath = "";

                xlApp.DisplayAlerts = true;

                xlApp.SheetsInNewWorkbook = 1;

                Microsoft.Office.Interop.Excel.Workbook xlBook = xlApp.Workbooks.Add(true);

                //将ListView的列名导入Excel表第一行

                foreach (ColumnHeader dc in listView.Columns)
                {

                    columnIndex++;

                    xlApp.Cells[rowIndex, columnIndex] = dc.Text;

                }

                //将ListView中的数据导入Excel中

                for (int i = 0; i < rowNum; i++)
                {

                    rowIndex++;

                    columnIndex = 0;

                    for (int j = 0; j < columnNum; j++)
                    {

                        columnIndex++;

                        //注意这个在导出的时候加了“\t” 的目的就是避免导出的数据显示为科学计数法。可以放在每行的首尾。

                        xlApp.Cells[rowIndex, columnIndex] = Convert.ToString(listView.Items[i].SubItems[j].Text) + "\t";

                    }

                }

                //例外需要说明的是用strFileName,Excel.XlFileFormat.xlExcel9795保存方式时 当你的Excel版本不是95、97 而是2003、2007 时导出的时候会报一个错误：异常来自 HRESULT:0x800A03EC。 解决办法就是换成strFileName, Microsoft.Office.Interop.Excel.XlFileFormat.xlWorkbookNormal。

                xlBook.SaveAs(strFileName, Microsoft.Office.Interop.Excel.XlFileFormat.xlWorkbookNormal, Type.Missing, Type.Missing, false, Type.Missing, Microsoft.Office.Interop.Excel.XlSaveAsAccessMode.xlExclusive, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing);
                xlBook.Close();
                xlApp = null;

                xlBook = null;

                MessageBox.Show("生成报表完成");

            }

        }
        protected override void OnFormClosing(FormClosingEventArgs e)//关闭所有线程
        {
            serialPort1.Close();
            this.Dispose();
            this.Close();
            System.Environment.Exit(0);
        }

        /*
         * 双击主面板上的chart控件，会呼叫出新的独立的chart面板
         */
        private void leftup_chart_DoubleClick(object sender, EventArgs e)
        {
            if (flag_pandushujushifouyouxiao == false)
                return;
            myDel = new chart_delegate(timeDomain_chart_display);
            Form2 displayer = new Form2(myDel, index_Global, shuju_chart1_1_erwei, shuju_chart1_2_erwei);
            displayer.Show();
        }

        private void rightup_chart_DoubleClick(object sender, EventArgs e)
        {
            if (flag_pandushujushifouyouxiao == false)
                return;
            myDel = new chart_delegate(Hz_fuZhi_chart_display);
            Form2 displayer = new Form2(myDel, index_Global, shuju_chart3_1_erwei, shuju_chart3_2_erwei);
            displayer.Show();
        }

        private void leftdown_chart_DoubleClick(object sender, EventArgs e)
        {
            if (flag_pandushujushifouyouxiao == false)
                return;
            myDel = new chart_delegate(cixinhao_chart_display);
            Form2 displayer = new Form2(myDel, index_Global, shuju_chart2_ci_erwei, null);
            displayer.Show();
        }

        private void rightdown_chart_DoubleClick(object sender, EventArgs e)
        {
            if (flag_pandushujushifouyouxiao == false)
                return;
            myDel = new chart_delegate(Hz_fuZhiCha_chart_display);
            Form2 displayer = new Form2(myDel, index_Global, shuju_chart3_1_erwei, shuju_chart3_2_erwei);
            displayer.Show();
        }

        private void choose_which_button_to_use(int flag)
        {
            //openserial_button.Enabled = ((flag & 0x0001) != 0) ? true : false;
            //clearserial_button.Enabled = ((flag & 0x0002) != 0) ? true : false;
            //readxinxi_button.Enabled = ((flag & 0x0004) != 0) ? true : false;
            //readshuju_button.Enabled = ((flag & 0x0008) != 0) ? true : false;

            //saveshuju_button.Enabled = ((flag & 0x0010) != 0) ? true : false;
            ////jiexishuju_button.Enabled = ((flag & 0x0020) != 0) ? true : false;
            //shengchengbaobiao_button.Enabled = ((flag & 0x0040) != 0) ? true : false;
            //saveimage_button.Enabled = ((flag & 0x0080) != 0) ? true : false;

            //clearflash_button.Enabled = ((flag & 0x0100) != 0) ? true : false;
            //download_button.Enabled = ((flag & 0x0200) != 0) ? true : false;
            //writexuliehao_button.Enabled = ((flag & 0x0400) != 0) ? true : false;
            //writechufayuzhi_button.Enabled = ((flag & 0x0800) != 0) ? true : false;

            //writegongzuomoshi_button.Enabled = ((flag & 0x1000) != 0) ? true : false;
            //writecichufayuzhi_button.Enabled = ((flag & 0x2000) != 0) ? true : false;
        }

        private int shujufenpei(List<byte> rec_Buffer_Global_xingcan, List<List<byte>> shuju_listview_erwei_xingcan, List<List<byte>> shuju_chart1_1_erwei_xingcan,
                                 List<List<byte>> shuju_chart1_2_erwei_xingcan, List<List<byte>> shuju_chart2_ci_erwei_xingcan, List<List<byte>> shuju_chart3_1_erwei_xingcan,
                                 List<List<byte>> shuju_chart3_2_erwei_xingcan, int zongshu_rec_xingcan)
        {
            //int zhennum_int = 0;//记录有几次数据
            List<int> zhen_list_int = new List<int>();//记录每个帧头的角标,同时list中的count也可以当作记录个数的作用
            int temp = 0;
            int list_end_int = 0;

            //rec_Buffer_Global_xingcan.Clear();//将相关数组清零
            shuju_listview_erwei_xingcan.Clear();
            shuju_chart1_1_erwei_xingcan.Clear();
            shuju_chart1_2_erwei_xingcan.Clear();
            shuju_chart2_ci_erwei_xingcan.Clear();
            shuju_chart3_1_erwei_xingcan.Clear();
            shuju_chart3_2_erwei_xingcan.Clear();


            //首先查询表头
            for (int i = 0; (zongshu_rec_xingcan - i) >= 1024; i++)//如果剩下的数据不足768，说明连一次数据都不足，可以直接丢弃
            {
                if (rec_Buffer_Global_xingcan[i] == 0x00 && rec_Buffer_Global_xingcan[i + 1] == 0xaa && rec_Buffer_Global_xingcan[i + 2] == 0x00 && rec_Buffer_Global_xingcan[i + 3] == 0x5f &&
                    rec_Buffer_Global_xingcan[i + 4] == 0x00 && rec_Buffer_Global_xingcan[i + 5] == 0xaa && rec_Buffer_Global_xingcan[i + 6] == 0x00 && rec_Buffer_Global_xingcan[i + 7] == 0x5f &&
                    rec_Buffer_Global_xingcan[i + 8] == 0x00 && rec_Buffer_Global_xingcan[i + 9] == 0xaa && rec_Buffer_Global_xingcan[i + 10] == 0x00 && rec_Buffer_Global_xingcan[i + 11] == 0x5f)
                {
                    //zhennum_int++;
                    zhen_list_int.Add(i);
                    i += 11;//一旦符合，剩下的数据都不用检测了，但是for判断语句中有i++，所以这里只是加11，而不是加12
                }
            }

            for (int i = 0; i < zhen_list_int.Count; i++)
            {
                temp = zhen_list_int[i];

                //判断是否是最后一组数据，如果是最后一组数据，帧尾就不能用zhen_list_int[i + 1]表示。
                list_end_int = (i != (zhen_list_int.Count - 1)) ? zhen_list_int[i + 1] : zongshu_rec_xingcan;

                shuju_listview_erwei_xingcan.Add(rec_Buffer_Global_xingcan.GetRange(temp, 256));
                shuju_chart1_1_erwei_xingcan.Add(rec_Buffer_Global_xingcan.GetRange(temp + 256, 256));
                shuju_chart1_2_erwei_xingcan.Add(rec_Buffer_Global_xingcan.GetRange(temp + 256 + 256, 256));
                shuju_chart2_ci_erwei_xingcan.Add(rec_Buffer_Global_xingcan.GetRange(temp + 256 + 512, 128));
                shuju_chart3_1_erwei_xingcan.Add(rec_Buffer_Global_xingcan.GetRange(temp + 256 + 640, 4));
                shuju_chart3_2_erwei_xingcan.Add(rec_Buffer_Global_xingcan.GetRange(temp + 256 + 644, 4));

                for (int j = 1024; (list_end_int - j - temp) >= 768; j += 768)//如果剩下的数据不足768，直接舍弃
                {
                    shuju_chart1_1_erwei_xingcan[i].AddRange(rec_Buffer_Global_xingcan.GetRange(temp + j, 256));
                    shuju_chart1_2_erwei_xingcan[i].AddRange(rec_Buffer_Global_xingcan.GetRange(temp + j + 256, 256));
                    shuju_chart2_ci_erwei_xingcan[i].AddRange(rec_Buffer_Global_xingcan.GetRange(temp + j + 512, 128));
                    shuju_chart3_1_erwei_xingcan[i].AddRange(rec_Buffer_Global_xingcan.GetRange(temp + j + 640, 4));
                    shuju_chart3_2_erwei_xingcan[i].AddRange(rec_Buffer_Global_xingcan.GetRange(temp + j + 644, 4));
                }
            }

            return zhen_list_int.Count;//已接收的次数
        }

        private bool pandushujushifouyouxiao(double x)
        {
            return (x == Double.NaN) || (x >= 10000000000000000000000000000.0) || (x <= -10000000000000000000000000000.0) ? false : true;
        }

        private void display_shuju_listview(int rem_shuju_return_int_xingcan)
        {
            for (int i = 0; i < rem_shuju_return_int_xingcan; i++)
            {
                ListViewItem lvi = new ListViewItem();
                lvi.ImageIndex = 0;     //通过与imageList绑定，显示imageList中第i项图标

                //序列号
                byte[] tran_xuliehao = new byte[12];
                for (int j = 0; j < 12; j++)
                {
                    tran_xuliehao[j] = shuju_listview_erwei[i][j + 72];
                }
                lvi.Text = System.Text.Encoding.ASCII.GetString(tran_xuliehao).Substring(3);

                //序号
                byte[] tran_xuhao_zhu = new byte[4];
                byte[] tran_xuhao_cong = new byte[4];

                tran_xuhao_zhu[3] = shuju_listview_erwei[i][15];
                tran_xuhao_zhu[2] = shuju_listview_erwei[i][14];
                tran_xuhao_zhu[1] = shuju_listview_erwei[i][13];
                tran_xuhao_zhu[0] = shuju_listview_erwei[i][12];

                tran_xuhao_cong[3] = shuju_listview_erwei[i][87];
                tran_xuhao_cong[2] = shuju_listview_erwei[i][86];
                tran_xuhao_cong[1] = shuju_listview_erwei[i][85];
                tran_xuhao_cong[0] = shuju_listview_erwei[i][84];
                lvi.SubItems.Add(BitConverter.ToInt32(tran_xuhao_zhu, 0).ToString() + "." + BitConverter.ToInt32(tran_xuhao_cong, 0));

                //日期,此段程序有待改进
                byte[] tran_nian = new byte[4];
                byte[] tran_yue = new byte[4];
                byte[] tran_ri = new byte[4];
                int nian, yue, ri;
                for (int j = 0; j < 4; j++)
                {
                    tran_nian[j] = shuju_listview_erwei[i][j + 16];//年
                    tran_yue[j] = shuju_listview_erwei[i][j + 20];//月
                    tran_ri[j] = shuju_listview_erwei[i][j + 24];//日
                }
                nian = tran_nian[0] + tran_nian[1] * 256 + tran_nian[2] * 65536;
                yue = tran_yue[0] + tran_yue[1] * 256 + tran_yue[2] * 65536;
                ri = tran_ri[0] + tran_ri[1] * 256 + tran_ri[2] * 65536;
                //lvi.SubItems.Add(nian.ToString() + "." + yue.ToString() + "." + ri.ToString());
                lvi.SubItems.Add(nian.ToString() + "." + string.Format("{0:D2}", yue) + "." + string.Format("{0:D2}", ri));

                //时间
                int shi, fen, miao, haomiao;
                shi = shuju_listview_erwei[i][28];
                fen = shuju_listview_erwei[i][32];
                miao = shuju_listview_erwei[i][36];
                haomiao = shuju_listview_erwei[i][40];
                //lvi.SubItems.Add(shi.ToString() + "." + fen.ToString() + "." + miao.ToString() + "." + haomiao.ToString());
                lvi.SubItems.Add(string.Format("{0:D2}", shi) + ":" + string.Format("{0:D2}", fen) + ":" + string.Format("{0:D2}", miao) + ":" + string.Format("{0:D3}", haomiao));

                //地理位置
                double jing, wei;
                jing = shuju_listview_erwei[i][48] + shuju_listview_erwei[i][49] * 256 + shuju_listview_erwei[i][50] * 65536 + shuju_listview_erwei[i][51] * 16777216;
                wei = shuju_listview_erwei[i][44] + shuju_listview_erwei[i][45] * 256 + shuju_listview_erwei[i][46] * 65536 + shuju_listview_erwei[i][47] * 16777216;
                //lvi.SubItems.Add("E" + jing.ToString() + " N" + wei.ToString());
                jing /= 10000000;
                wei /= 10000000;
                lvi.SubItems.Add("E" + string.Format("{0:N7}", jing) + " N" + string.Format("{0:N7}", wei));

                //this.shuju_listView.Items.Add(lvi);
                Action<string> actionlistviewadd = (x) => { this.shuju_listView.Items.Add(lvi); };
                this.shuju_listView.Invoke(actionlistviewadd, "Over");
            }
        }

        /*********************************************************************************/
        chart_delegate myDel = null;
        public int timeDomain_chart_display(Chart leftup_chart, int index, List<List<byte>> shuju_chart1_1_erwei, List<List<byte>> shuju_chart1_2_erwei)
        {
            double temp = 0;
            double max, min;

            leftup_chart.Titles.Clear();
            leftup_chart.Titles.Add("时域波形");
            leftup_chart.ChartAreas[0].AxisX.Title = "采样点/个";
            leftup_chart.ChartAreas[0].AxisY.Title = "幅值/V";

            leftup_chart.Series.Clear();

            Series serial_ch1 = new Series("水平");
            serial_ch1.ChartType = SeriesChartType.Spline;
            serial_ch1.Color = Color.Red;

            Series serial_ch2 = new Series("垂直");
            serial_ch2.ChartType = SeriesChartType.Spline;
            serial_ch2.Color = Color.Black;

            max = 0; min = 2147483647;
            for (int i = 0; i < shuju_chart1_1_erwei[index].Count - 12; i += 12)//不能每个点都显示，都显示的话控件太卡（滚动条有明显的延迟）
            {
                temp = (shuju_chart1_1_erwei[index][i] * 16777216 + shuju_chart1_1_erwei[index][i + 1] * 65536 + shuju_chart1_1_erwei[index][i + 2] * 256 + shuju_chart1_1_erwei[index][i + 3] +
                       shuju_chart1_1_erwei[index][i + 4] * 16777216 + shuju_chart1_1_erwei[index][i + 5] * 65536 + shuju_chart1_1_erwei[index][i + 6] * 256 + shuju_chart1_1_erwei[index][i + 7] +
                       shuju_chart1_1_erwei[index][i + 8] * 16777216 + shuju_chart1_1_erwei[index][i + 9] * 65536 + shuju_chart1_1_erwei[index][i + 10] * 256 + shuju_chart1_1_erwei[index][i + 11]) / 3;

                temp = (temp - 8388607) * 2.5 / 8388608;

                serial_ch1.Points.AddXY(i / 4, temp);
                max = (temp > max) ? temp : max;
                min = (temp < min) ? temp : min;
                //temp = shuju_chart1_2_erwei[index][i] * 16777216 + shuju_chart1_2_erwei[index][i + 1] * 65536 + shuju_chart1_2_erwei[index][i + 2] * 256 + shuju_chart1_2_erwei[index][i + 3];
                temp = (shuju_chart1_2_erwei[index][i] * 16777216 + shuju_chart1_2_erwei[index][i + 1] * 65536 + shuju_chart1_2_erwei[index][i + 2] * 256 + shuju_chart1_2_erwei[index][i + 3] +
                       shuju_chart1_2_erwei[index][i + 4] * 16777216 + shuju_chart1_2_erwei[index][i + 5] * 65536 + shuju_chart1_2_erwei[index][i + 6] * 256 + shuju_chart1_2_erwei[index][i + 7] +
                       shuju_chart1_2_erwei[index][i + 8] * 16777216 + shuju_chart1_2_erwei[index][i + 9] * 65536 + shuju_chart1_2_erwei[index][i + 10] * 256 + shuju_chart1_2_erwei[index][i + 11]) / 3;

                temp = (temp - 8388607) * 2.5 / 8388608;

                serial_ch2.Points.AddXY(i / 4, temp);////////////////////////////////////////////
                max = (temp > max) ? temp : max;
                min = (temp < min) ? temp : min;
            }

            if (max != min)
            {
                leftup_chart.ChartAreas[0].AxisY.Maximum = max + (max - min) / 5;
                leftup_chart.ChartAreas[0].AxisY.Minimum = min - (max - min) / 5;
            }
            else
            {
                leftup_chart.ChartAreas[0].AxisY.Maximum = max + 5;
                leftup_chart.ChartAreas[0].AxisY.Minimum = min - 5;
            }

            leftup_chart.Series.Add(serial_ch1);
            leftup_chart.Series.Add(serial_ch2);

            return 0;//不对数据的有效性进行检测
        }

        public int cixinhao_chart_display(Chart rightup_chart, int index, List<List<byte>> shuju_chart2_ci_erwei, List<List<byte>> shuju_Null)
        {
            double temp = 0;
            double max, min;
            byte[] tran_byte_4 = new byte[8];

            rightup_chart.Series.Clear();

            rightup_chart.Titles.Clear();
            rightup_chart.Titles.Add("磁信号");
            rightup_chart.ChartAreas[0].AxisX.Title = "采样点/个";
            rightup_chart.ChartAreas[0].AxisY.Title = "幅值/V";


            Series rightup_chart_ch = new Series("磁信号");
            rightup_chart_ch.ChartType = SeriesChartType.Spline;
            rightup_chart_ch.Color = Color.Red;

            max = -2147483647; min = 2147483647;

            double temp_tran_1 = 0, temp_tran_2 = 0, temp_tran_3 = 0;

            for (int i = 0; i < shuju_chart2_ci_erwei[index].Count - 12; i += 12)//不能每个点都显示，都显示的话控件太卡（滚动条有明显的延迟）
            {
                //temp = (shuju_chart2_ci_erwei[index][i] * 16777216 + shuju_chart2_ci_erwei[index][i + 1] * 65536 + shuju_chart2_ci_erwei[index][i + 2] * 256 + shuju_chart2_ci_erwei[index][i + 3] +
                //       shuju_chart2_ci_erwei[index][i + 4] * 16777216 + shuju_chart2_ci_erwei[index][i + 5] * 65536 + shuju_chart2_ci_erwei[index][i + 6] * 256 + shuju_chart2_ci_erwei[index][i + 7] +
                //       shuju_chart2_ci_erwei[index][i + 8] * 16777216 + shuju_chart2_ci_erwei[index][i + 9] * 65536 + shuju_chart2_ci_erwei[index][i + 10] * 256 + shuju_chart2_ci_erwei[index][i + 11]) / 3;

                tran_byte_4[0] = shuju_chart2_ci_erwei[index][i + 3];
                tran_byte_4[1] = shuju_chart2_ci_erwei[index][i + 2];
                tran_byte_4[2] = shuju_chart2_ci_erwei[index][i + 1];
                tran_byte_4[3] = shuju_chart2_ci_erwei[index][i];
                temp_tran_1 = BitConverter.ToSingle(tran_byte_4, 0);

                tran_byte_4[0] = shuju_chart2_ci_erwei[index][i + 7];
                tran_byte_4[1] = shuju_chart2_ci_erwei[index][i + 6];
                tran_byte_4[2] = shuju_chart2_ci_erwei[index][i + 5];
                tran_byte_4[3] = shuju_chart2_ci_erwei[index][i + 4];
                temp_tran_2 = BitConverter.ToSingle(tran_byte_4, 0);

                tran_byte_4[0] = shuju_chart2_ci_erwei[index][i + 11];
                tran_byte_4[1] = shuju_chart2_ci_erwei[index][i + 10];
                tran_byte_4[2] = shuju_chart2_ci_erwei[index][i + 9];
                tran_byte_4[3] = shuju_chart2_ci_erwei[index][i + 8];
                temp_tran_3 = BitConverter.ToSingle(tran_byte_4, 0);

                temp = (temp_tran_1 + temp_tran_2 + temp_tran_3) / 3;

                if (pandushujushifouyouxiao(temp) == false)
                {
                    flag_pandushujushifouyouxiao = false;
                    MessageBox.Show("由于本次数据出错\r\n导致数据数值过大无法解析");
                    return 1;//数据不正常解析
                }

                rightup_chart_ch.Points.AddXY(i / 4, temp);//////////////////////////////////////
                max = (temp > max) ? temp : max;
                min = (temp < min) ? temp : min;
            }

            if (max != min)
            {
                rightup_chart.ChartAreas[0].AxisY.Maximum = max + (max - min) / 5;
                rightup_chart.ChartAreas[0].AxisY.Minimum = min - (max - min) / 5;
            }
            else
            {
                rightup_chart.ChartAreas[0].AxisY.Maximum = max + 5;
                rightup_chart.ChartAreas[0].AxisY.Minimum = min - 5;
            }

            rightup_chart.Series.Add(rightup_chart_ch);

            return 0;//数据正常解析
        }

        public int Hz_fuZhi_chart_display(Chart leftdown_chart, int index, List<List<byte>> shuju_chart3_1_erwei, List<List<byte>> shuju_chart3_2_erwei)
        {
            double temp = 0;
            double max, min;
            byte[] tran_byte_4 = new byte[8];

            leftdown_chart.Titles.Clear();
            leftdown_chart.Titles.Add("22Hz幅值");
            leftdown_chart.ChartAreas[0].AxisX.Title = "频域点/个";
            leftdown_chart.ChartAreas[0].AxisY.Title = "幅值比/dB";

            leftdown_chart.Series.Clear();

            Series leftdown_chart_ch1 = new Series("水平");
            leftdown_chart_ch1.ChartType = SeriesChartType.Spline;
            leftdown_chart_ch1.Color = Color.Red;

            Series leftdown_chart_ch2 = new Series("垂直");
            leftdown_chart_ch2.ChartType = SeriesChartType.Spline;
            leftdown_chart_ch2.Color = Color.Black;

            max = -2147483647; min = 2147483647;

            for (int i = 0; i < shuju_chart3_1_erwei[index].Count; i += 4)//不能每个点都显示，都显示的话控件太卡（滚动条有明显的延迟）
            {
                //temp = shuju_chart3_1_erwei[index][i] * 16777216 + shuju_chart3_1_erwei[index][i + 1] * 65536 + shuju_chart3_1_erwei[index][i + 2] * 256 + shuju_chart3_1_erwei[index][i + 3];
                tran_byte_4[0] = shuju_chart3_1_erwei[index][i + 3];
                tran_byte_4[1] = shuju_chart3_1_erwei[index][i + 2];
                tran_byte_4[2] = shuju_chart3_1_erwei[index][i + 1];
                tran_byte_4[3] = shuju_chart3_1_erwei[index][i];
                temp = BitConverter.ToSingle(tran_byte_4, 0);

                if (pandushujushifouyouxiao(temp) == false)
                {
                    flag_pandushujushifouyouxiao = false;
                    MessageBox.Show("由于本次数据出错\r\n导致数据数值过大无法解析");
                    return 1;//数据不正常解析
                }

                leftdown_chart_ch1.Points.AddXY(i / 4, temp);

                max = (temp > max) ? temp : max;
                min = (temp < min) ? temp : min;
                //temp = shuju_chart1_2_erwei[index][i] * 16777216 + shuju_chart1_2_erwei[index][i + 1] * 65536 + shuju_chart1_2_erwei[index][i + 2] * 256 + shuju_chart1_2_erwei[index][i + 3];
                //temp = shuju_chart3_2_erwei[index][i] * 16777216 + shuju_chart3_2_erwei[index][i + 1] * 65536 + shuju_chart3_2_erwei[index][i + 2] * 256 + shuju_chart3_2_erwei[index][i + 3];
                tran_byte_4[0] = shuju_chart3_2_erwei[index][i + 3];
                tran_byte_4[1] = shuju_chart3_2_erwei[index][i + 2];
                tran_byte_4[2] = shuju_chart3_2_erwei[index][i + 1];
                tran_byte_4[3] = shuju_chart3_2_erwei[index][i];
                temp = BitConverter.ToSingle(tran_byte_4, 0);

                if (pandushujushifouyouxiao(temp) == false)
                {
                    flag_pandushujushifouyouxiao = false;
                    MessageBox.Show("由于本次数据出错\r\n导致数据数值过大无法解析");
                    return 1;//数据不正常解析
                }

                leftdown_chart_ch2.Points.AddXY(i / 4, temp);
                max = (temp > max) ? temp : max;
                min = (temp < min) ? temp : min;
            }

            if (max != min)
            {
                leftdown_chart.ChartAreas[0].AxisY.Maximum = max + (max - min) / 5;
                leftdown_chart.ChartAreas[0].AxisY.Minimum = min - (max - min) / 5;
            }
            else
            {
                leftdown_chart.ChartAreas[0].AxisY.Maximum = max + 5;
                leftdown_chart.ChartAreas[0].AxisY.Minimum = min - 5;
            }

            leftdown_chart.Series.Add(leftdown_chart_ch1);
            leftdown_chart.Series.Add(leftdown_chart_ch2);

            return 0;//数据正常解析
        }

        public int Hz_fuZhiCha_chart_display(Chart rightdown_chart, int index, List<List<byte>> shuju_chart3_1_erwei, List<List<byte>> shuju_chart3_2_erwei)
        {
            double temp1, temp2;
            double max, min;
            byte[] tran_byte_4 = new byte[8];

            rightdown_chart.Titles.Clear();
            rightdown_chart.Titles.Add("22Hz幅值之差");
            rightdown_chart.ChartAreas[0].AxisX.Title = "频域点/个";
            rightdown_chart.ChartAreas[0].AxisY.Title = "幅值比/dB";

            rightdown_chart.Series.Clear();


            Series rightdown_chart_ch2 = new Series("差值");
            rightdown_chart_ch2.ChartType = SeriesChartType.Spline;
            rightdown_chart_ch2.Color = Color.Black;

            max = -2147483647; min = 2147483647;
            for (int i = 0; i < shuju_chart3_1_erwei[index].Count; i += 4)//不能每个点都显示，都显示的话控件太卡（滚动条有明显的延迟）
            {
                //temp1 = shuju_chart3_1_erwei[index][i] * 16777216 + shuju_chart3_1_erwei[index][i + 1] * 65536 + shuju_chart3_1_erwei[index][i + 2] * 256 + shuju_chart3_1_erwei[index][i + 3];

                //temp2 = shuju_chart3_2_erwei[index][i] * 16777216 + shuju_chart3_2_erwei[index][i + 1] * 65536 + shuju_chart3_2_erwei[index][i + 2] * 256 + shuju_chart3_2_erwei[index][i + 3];

                tran_byte_4[0] = shuju_chart3_1_erwei[index][i + 3];
                tran_byte_4[1] = shuju_chart3_1_erwei[index][i + 2];
                tran_byte_4[2] = shuju_chart3_1_erwei[index][i + 1];
                tran_byte_4[3] = shuju_chart3_1_erwei[index][i];
                temp1 = BitConverter.ToSingle(tran_byte_4, 0);

                tran_byte_4[0] = shuju_chart3_2_erwei[index][i + 3];
                tran_byte_4[1] = shuju_chart3_2_erwei[index][i + 2];
                tran_byte_4[2] = shuju_chart3_2_erwei[index][i + 1];
                tran_byte_4[3] = shuju_chart3_2_erwei[index][i];
                temp2 = BitConverter.ToSingle(tran_byte_4, 0);


                rightdown_chart_ch2.Points.AddXY(i / 4, (temp1 - temp2));


                max = ((temp1 - temp2) > max) ? (temp1 - temp2) : max;
                min = ((temp1 - temp2) < min) ? (temp1 - temp2) : min;
            }

            if (max != min)
            {
                rightdown_chart.ChartAreas[0].AxisY.Maximum = max + (max - min) / 5;
                rightdown_chart.ChartAreas[0].AxisY.Minimum = min - (max - min) / 5;
            }
            else
            {
                rightdown_chart.ChartAreas[0].AxisY.Maximum = max + 5;
                rightdown_chart.ChartAreas[0].AxisY.Minimum = min - 5;
            }

            rightdown_chart.Series.Add(rightdown_chart_ch2);

            return 0;//由于此函数数据在之前的图标正已经使用过了，所以本函数不对数据的有效性进行检测
        }

        int shuju_jieshouwanquan_or_not(int rem_shuju_return_int_xingcan, int chufacishu_int_xingcan, List<byte> rec_Buffer_Global_xingcan)
        {
            //if (rec_Buffer_Global_xingcan.Count < 8)//如果连1024都不够，肯定不是一组完整的数据，数据接收中断
            //    return 1;            
            if (rem_shuju_return_int_xingcan != chufacishu_int_xingcan)
                return 1;
            if (rec_Buffer_Global_xingcan[rec_Buffer_Global_xingcan.Count - 1] != 0xff | rec_Buffer_Global_xingcan[rec_Buffer_Global_xingcan.Count - 2] != 0xff | rec_Buffer_Global_xingcan[rec_Buffer_Global_xingcan.Count - 3] != 0xff | rec_Buffer_Global_xingcan[rec_Buffer_Global_xingcan.Count - 4] != 0xff |
                rec_Buffer_Global_xingcan[rec_Buffer_Global_xingcan.Count - 5] != 0xff | rec_Buffer_Global_xingcan[rec_Buffer_Global_xingcan.Count - 6] != 0xff | rec_Buffer_Global_xingcan[rec_Buffer_Global_xingcan.Count - 7] != 0xff | rec_Buffer_Global_xingcan[rec_Buffer_Global_xingcan.Count - 8] != 0xff)
                return 1;
            return 0;
        }

        int combox_of_serial()
        {
            string[] com_str_arry = SerialPort.GetPortNames();

            Action<bool> actiondelegate1 = (x) => { this.serialport_comboBox.Items.Clear(); };
            this.serialport_comboBox.Invoke(actiondelegate1, true);

            Action<string> actiondelegate = (x) => { this.serialport_comboBox.Items.Add(x); };

            if (com_str_arry.Length == 0)
            {
                Action<bool> actiondelegate2 = (x) => { this.serialport_comboBox.Text = ""; };
                this.serialport_comboBox.Invoke(actiondelegate2, true);

                return 0;
            }

            for (int i = 0; i < com_str_arry.Length; i++)
            {
                //serialport_comboBox_xingcan.Items.Add(com_str_arry[i]);

                this.serialport_comboBox.Invoke(actiondelegate, com_str_arry[i]);
            }

            return 0;
        }
    }
    /**********************************委托******************************************/
    public delegate int chart_delegate(Chart chart, int index, List<List<byte>> shuju_chart1_1_erwei, List<List<byte>> shuju_chart1_2_erwei);
}
