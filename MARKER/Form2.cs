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

namespace MARKER
{
    public partial class Form2 : Form
    {
        List<List<byte>> chart1_erwei_tt;
        List<List<byte>> chart2_erwei_tt;
        int index_tt = 0;

        public Form2(List<List<byte>> chart1_erwei, List<List<byte>> chart2_erwei, int index)
        {
            InitializeComponent();
            chart1_erwei_tt = chart1_erwei;
            chart2_erwei_tt = chart2_erwei;
            index_tt = index;

            chart1.ChartAreas[0].AxisY.LabelStyle.Format = "N2";
            chart1.Titles.Add("Time Domain");

            /**********************************************leftup_chart*****************************************************************/
            chart1.Series.Clear();
            chart1.ChartAreas[0].CursorX.IsUserEnabled = true;
            chart1.ChartAreas[0].CursorX.IsUserSelectionEnabled = true;
            chart1.ChartAreas[0].AxisX.ScaleView.Zoomable = true;

            chart1.ChartAreas[0].AxisX.ScrollBar.IsPositionedInside = true;
            chart1.ChartAreas[0].AxisX.ScrollBar.Size = 10;///////
            chart1.ChartAreas[0].AxisX.ScrollBar.ButtonStyle = ScrollBarButtonStyles.All;

            chart1.ChartAreas[0].AxisX.ScaleView.SmallScrollSize = double.NaN;
            chart1.ChartAreas[0].AxisX.ScaleView.SmallScrollSize = 2;

            chart1.ChartAreas[0].AxisY.Maximum = System.Double.NaN;
            chart1.ChartAreas[0].AxisY.Minimum = System.Double.NaN;

            chart1.ChartAreas[0].CursorY.IsUserEnabled = true;
            chart1.ChartAreas[0].CursorY.IsUserSelectionEnabled = true;
            chart1.ChartAreas[0].AxisY.ScaleView.Zoomable = true;

            chart1.ChartAreas[0].AxisY.ScrollBar.IsPositionedInside = true;
            chart1.ChartAreas[0].AxisY.ScrollBar.Size = 10;///////
            chart1.ChartAreas[0].AxisY.ScrollBar.ButtonStyle = ScrollBarButtonStyles.All;

            chart1.ChartAreas[0].AxisY.ScaleView.SmallScrollSize = double.NaN;
            chart1.ChartAreas[0].AxisY.ScaleView.SmallScrollSize = 2;
            /***********************************************************************************/
            double temp = 0;
            double temp_double = 0;
            double max, min;
            //index = this.shuju_listView.SelectedItems[0].Index;//不知道为什么要是0，但事实证明0是好使的

            chart1.Series.Clear();

            Series serial_ch1 = new Series("horizontal");
            serial_ch1.ChartType = SeriesChartType.Spline;
            serial_ch1.Color = Color.Red;

            Series serial_ch2 = new Series("vertical");
            serial_ch2.ChartType = SeriesChartType.Spline;
            serial_ch2.Color = Color.Black;

            max = 0; min = 2147483647;
            for (int i = 0; i < chart1_erwei_tt[index_tt].Count - 12; i += 12)//不能每个点都显示，都显示的话控件太卡（滚动条有明显的延迟）
            {
                temp = (chart1_erwei_tt[index_tt][i] * 16777216 + chart1_erwei_tt[index_tt][i + 1] * 65536 + chart1_erwei_tt[index_tt][i + 2] * 256 + chart1_erwei_tt[index_tt][i + 3] +
                       chart1_erwei_tt[index_tt][i + 4] * 16777216 + chart1_erwei_tt[index_tt][i + 5] * 65536 + chart1_erwei_tt[index_tt][i + 6] * 256 + chart1_erwei_tt[index_tt][i + 7] +
                       chart1_erwei_tt[index_tt][i + 8] * 16777216 + chart1_erwei_tt[index_tt][i + 9] * 65536 + chart1_erwei_tt[index_tt][i + 10] * 256 + chart1_erwei_tt[index_tt][i + 11]) / 3;
                //temp_double = temp / 1000000;
                temp_double = (temp - 8388607) * 2.5 / 8388608;
                serial_ch1.Points.AddXY(i/4, temp_double);
                max = (temp_double > max) ? temp_double : max;
                min = (temp_double < min) ? temp_double : min;
                //temp = shuju_chart1_2_erwei[index][i] * 16777216 + shuju_chart1_2_erwei[index][i + 1] * 65536 + shuju_chart1_2_erwei[index][i + 2] * 256 + shuju_chart1_2_erwei[index][i + 3];
                temp = (chart2_erwei_tt[index_tt][i] * 16777216 + chart2_erwei_tt[index_tt][i + 1] * 65536 + chart2_erwei_tt[index_tt][i + 2] * 256 + chart2_erwei_tt[index_tt][i + 3] +
                       chart2_erwei_tt[index_tt][i + 4] * 16777216 + chart2_erwei_tt[index_tt][i + 5] * 65536 + chart2_erwei_tt[index_tt][i + 6] * 256 + chart2_erwei_tt[index_tt][i + 7] +
                       chart2_erwei_tt[index_tt][i + 8] * 16777216 + chart2_erwei_tt[index_tt][i + 9] * 65536 + chart2_erwei_tt[index_tt][i + 10] * 256 + chart2_erwei_tt[index_tt][i + 11]) / 3;
                //temp_double = temp / 1000000;
                temp_double = (temp - 8388607) * 2.5 / 8388608;
                serial_ch2.Points.AddXY(i/4, temp_double);
                max = (temp_double > max) ? temp_double : max;
                min = (temp_double < min) ? temp_double : min;
            }

            chart1.ChartAreas[0].AxisY.Maximum = max + (max - min) / 5;
            chart1.ChartAreas[0].AxisY.Minimum = min - (max - min) / 5;

            chart1.Series.Add(serial_ch1);
            chart1.Series.Add(serial_ch2);
        }

        public Form2(chart_delegate chart_delegate_xingcan, int index, List<List<byte>> chart_ch1, List<List<byte>> chart_ch2)
        {
            InitializeComponent();
            index_tt = index;

            chart1.ChartAreas[0].AxisY.LabelStyle.Format = "N2";
            chart1.Titles.Add("Time Domain");

            /**********************************************leftup_chart*****************************************************************/
            chart1.Series.Clear();
            chart1.ChartAreas[0].CursorX.IsUserEnabled = true;
            chart1.ChartAreas[0].CursorX.IsUserSelectionEnabled = true;
            chart1.ChartAreas[0].AxisX.ScaleView.Zoomable = true;

            chart1.ChartAreas[0].AxisX.ScrollBar.IsPositionedInside = true;
            chart1.ChartAreas[0].AxisX.ScrollBar.Size = 10;///////
            chart1.ChartAreas[0].AxisX.ScrollBar.ButtonStyle = ScrollBarButtonStyles.All;

            chart1.ChartAreas[0].AxisX.ScaleView.SmallScrollSize = double.NaN;
            chart1.ChartAreas[0].AxisX.ScaleView.SmallScrollSize = 2;

            chart1.ChartAreas[0].AxisY.Maximum = System.Double.NaN;
            chart1.ChartAreas[0].AxisY.Minimum = System.Double.NaN;

            chart1.ChartAreas[0].CursorY.IsUserEnabled = true;
            chart1.ChartAreas[0].CursorY.IsUserSelectionEnabled = true;
            chart1.ChartAreas[0].AxisY.ScaleView.Zoomable = true;

            chart1.ChartAreas[0].AxisY.ScrollBar.IsPositionedInside = true;
            chart1.ChartAreas[0].AxisY.ScrollBar.Size = 10;///////
            chart1.ChartAreas[0].AxisY.ScrollBar.ButtonStyle = ScrollBarButtonStyles.All;

            chart1.ChartAreas[0].AxisY.ScaleView.SmallScrollSize = double.NaN;
            chart1.ChartAreas[0].AxisY.ScaleView.SmallScrollSize = 2;

            if (chart_delegate_xingcan(chart1, index, chart_ch1, chart_ch2) != 0)//对数据的有效性进行检测
                return;
        }

        private void chart1_DoubleClick(object sender, EventArgs e)
        {
            System.Windows.Forms.SaveFileDialog sfd = new SaveFileDialog();//注意 这里是SaveFileDialog,不是OpenFileDialog
            sfd.DefaultExt = "jpeg";
            sfd.Filter = "图片(*.jpeg)|*.jpeg";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                //string[] temp_str = sfd.FileName.Split('.');//以‘。’为分割符，分成两份，取前一份
                //总共四个图片，要保存四份，添加了一定的命名规律
                chart1.SaveImage(sfd.FileName, System.Drawing.Imaging.ImageFormat.Jpeg);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)//关闭所有线程
        {
            this.Dispose();
            this.Close();
        }
    }
}
