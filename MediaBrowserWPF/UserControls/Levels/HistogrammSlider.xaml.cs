using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MediaBrowserWPF.UserControls.Levels
{
    /// <summary>
    /// Interaktionslogik für HistogrammSlider.xaml
    /// </summary>
    public partial class HistogrammSlider : UserControl
    {
        public class LevelsValueChangedArgs : EventArgs
        {
            public HistoRemap HistoRemap { get; set; }
        }

        public event EventHandler<LevelsValueChangedArgs> LevelsValueChanged;

        public HistogrammSlider()
        {
            InitializeComponent();
            this.Reset();
        }

        public void Reset()
        {
            this.histoRemap.Reset();
            this.SetHistoRemap();            
        }

        public void Add(HistoRemap histoRemap)
        {
            this.histoRemap.Add(histoRemap);
            this.SetHistoRemap();           
        }

        public double InputWhiteValue
        {
            get { return (double)GetValue(InputWhiteValueProperty); }
            set { SetValue(InputWhiteValueProperty, value); }
        }

        public static readonly DependencyProperty InputWhiteValueProperty =
            DependencyProperty.Register("InputWhiteSlider", typeof(double), typeof(HistogrammSlider), new UIPropertyMetadata(0d));

        public double InputGrayValue
        {
            get { return (double)GetValue(InputGrayValueProperty); }
            set { SetValue(InputGrayValueProperty, value); }
        }

        public static readonly DependencyProperty InputGrayValueProperty =
            DependencyProperty.Register("InputGraySlider", typeof(double), typeof(HistogrammSlider), new UIPropertyMetadata(0d));

        public double InputBlackValue
        {
            get { return (double)GetValue(InputBlackValueProperty); }
            set { SetValue(InputBlackValueProperty, value); }
        }

        public static readonly DependencyProperty InputBlackValueProperty =
          DependencyProperty.Register("InputBlackSlider", typeof(double), typeof(HistogrammSlider), new UIPropertyMetadata(0d));

        public double OutputBlackValue
        {
            get { return (double)GetValue(OutputBlackValueProperty); }
            set { SetValue(OutputBlackValueProperty, value); }
        }

        public static readonly DependencyProperty OutputBlackValueProperty =
          DependencyProperty.Register("OutputBlackSlider", typeof(double), typeof(HistogrammSlider), new UIPropertyMetadata(0d));

        public double OutputWhiteValue
        {
            get { return (double)GetValue(OutputWhiteValueProperty); }
            set { SetValue(OutputWhiteValueProperty, value); }
        }

        public static readonly DependencyProperty OutputWhiteValueProperty =
          DependencyProperty.Register("OutputWhiteSlider", typeof(double), typeof(HistogrammSlider), new UIPropertyMetadata(0d));

        bool isAdjusting;
        private void SetHistoRemap()
        {
            this.isAdjusting = true;

            if (this.LevelsValueChanged != null)
            {
                this.histoRemap.RemapAll();
                this.LevelsValueChanged.Invoke(this, new LevelsValueChangedArgs() { HistoRemap = histoRemap });
            }

            this.InputBlackSlider.Value = this.histoRemap.InputBlack;
            this.InputWhiteSlider.Value = this.histoRemap.InputWhite;
            this.InputGraySlider.Value = this.histoRemap.InputGray;
            this.OutputBlackSlider.Value = this.histoRemap.OutputBlack;
            this.OutputWhiteSlider.Value = this.histoRemap.OutputWhite;

            this.SetToolTip();

            this.isAdjusting = false;
        }

        HistoRemap histoRemap = new HistoRemap();
        public HistoRemap HistoRemap
        {
            set
            {
                this.histoRemap = value == null ? new HistoRemap() : (HistoRemap)value.Clone();
                this.SetHistoRemap();
                this.SetToolTip();
            }

            get
            {
                return this.histoRemap;
            }
        }

        private void  SetToolTip()
        {
            string outputToolTip = string.Format("{0} ... {1}", this.histoRemap.OutputBlack, this.histoRemap.OutputWhite);
            string inputToolTip = string.Format("{0} ... {1} ... {2}", this.histoRemap.InputBlack, this.histoRemap.InputGray, this.histoRemap.InputWhite);
            this.OutputBlackSlider.ToolTip = outputToolTip;
            this.OutputWhiteSlider.ToolTip = outputToolTip;
            this.InputBlackSlider.ToolTip = inputToolTip;
            this.InputGraySlider.ToolTip = inputToolTip;
            this.InputWhiteSlider.ToolTip = inputToolTip;
        }

        private void OutputBlackSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.isAdjusting) return;            
            this.histoRemap.OutputBlack = (int)this.OutputBlackValue;
            this.SetToolTip();
            this.SetHistoRemap();
        }

        private void OutputWhiteSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.isAdjusting) return;            
            this.histoRemap.OutputWhite = (int)this.OutputWhiteValue;
            this.SetToolTip();
            this.SetHistoRemap();
        }

        private void InputBlackSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.isAdjusting) return;
            this.histoRemap.InputBlack = (int)this.InputBlackValue;
            this.SetToolTip();
            this.SetHistoRemap();
        }

        private void InputGraySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.isAdjusting) return;
            this.histoRemap.InputGray = (int)this.InputGrayValue;
            this.SetToolTip();
            this.SetHistoRemap();
        }

        private void InputWhiteSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.isAdjusting) return;
            this.histoRemap.InputWhite = (int)this.InputWhiteValue;
            this.SetToolTip();
            this.SetHistoRemap();
        }
    }
}
