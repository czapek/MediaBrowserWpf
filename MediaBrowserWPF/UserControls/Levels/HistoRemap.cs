using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowserWPF.UserControls.Levels
{
    public class HistoRemap : ICloneable
    {
        enum RGB { Blue, Green, Red };

        private int[] remap = new int[256];      //remap levels for r, g or b or luminance values

        public double PshopGamma { get; private set; }

        private int inputBlack, inputGray, inputWhite, outputBlack, outputWhite;

        public int InputBlackDiff { get; private set; }
        public int InputGrayDiff { get; private set; }
        public int InputWhiteDiff { get; private set; }
        public int OutputBlackDiff { get; private set; }
        public int OutputWhiteDiff { get; private set; }

        public void Add(HistoRemap histoRemap)
        {
            this.outputBlack = this.AddDiffValue(this.outputBlack, histoRemap.OutputBlackDiff);
            this.outputWhite = this.AddDiffValue(this.outputWhite, histoRemap.OutputWhiteDiff);
            this.inputBlack = this.AddDiffValue(this.inputBlack, histoRemap.InputBlackDiff);
            this.inputGray = this.AddDiffValue(this.inputGray, histoRemap.InputGrayDiff);
            this.inputWhite = this.AddDiffValue(this.inputWhite, histoRemap.InputWhiteDiff);
        }

        private int AddDiffValue(int valBase, int vcalAdd)
        {
            int value = valBase + vcalAdd;
            if (value < 0) value = 0;
            if (value > 255) value = 255;

            return value;
        }

        public int OutputBlack
        {
            get
            {
                return this.outputBlack;
            }

            set
            {
                if (value < 0) value = 0;
                if (value > 255) value = 255;

                if (value > this.outputWhite) this.outputWhite = value;

                this.InputBlackDiff = 0;
                this.InputGrayDiff = 0;
                this.InputWhiteDiff = 0;
                this.OutputBlackDiff = value - this.OutputBlack;
                this.OutputWhiteDiff = 0;

                this.outputBlack = value;
            }
        }

        public int OutputWhite
        {
            get
            {
                return this.outputWhite;
            }

            set
            {
                if (value < 0) value = 0;
                if (value > 255) value = 255;

                if (value < this.outputBlack) this.outputBlack = value;

                this.InputBlackDiff = 0;
                this.InputGrayDiff = 0;
                this.InputWhiteDiff = 0;
                this.OutputBlackDiff = 0;
                this.OutputWhiteDiff = value - this.OutputWhite;

                this.outputWhite = value;
            }
        }

        public int InputBlack
        {
            get
            {
                return this.inputBlack;
            }

            set
            {
                if (value < 0) value = 0;
                if (value > 255) value = 255;

                if (value > this.inputGray) this.inputGray = value;
                if (value > this.inputWhite) this.inputWhite = value;

                this.InputBlackDiff = value - this.InputBlack;
                this.InputGrayDiff = 0;
                this.InputWhiteDiff = 0;
                this.OutputBlackDiff = 0;
                this.OutputWhiteDiff = 0;

                this.inputBlack = value;
            }
        }

        public double Gamma
        {
            set
            {

                this.InputGray = GammaRemap.GetGrayFromGamma(value);
            }

            get
            {
                return GammaRemap.GetGammFromGray(this.InputGray);
            }
        }

        public int InputGray
        {
            get
            {
                return this.inputGray;
            }

            set
            {
                if (value < 0) value = 0;
                if (value > 255) value = 255;

                if (value < this.inputBlack) this.inputBlack = value;
                if (value > this.inputWhite) this.inputWhite = value;

                this.InputBlackDiff = 0;
                this.InputGrayDiff = value - this.InputGray;
                this.InputWhiteDiff = 0;
                this.OutputBlackDiff = 0;
                this.OutputWhiteDiff = 0;

                this.inputGray = value;
            }
        }

        public int InputWhite
        {
            get
            {
                return this.inputWhite;
            }

            set
            {
                if (value < 0) value = 0;
                if (value > 255) value = 255;

                if (value < this.inputGray) this.inputGray = value;
                if (value < this.inputBlack) this.inputBlack = value;

                this.InputBlackDiff = 0;
                this.InputGrayDiff = 0;
                this.InputWhiteDiff = value - this.InputWhite;
                this.OutputBlackDiff = 0;
                this.OutputWhiteDiff = 0;

                this.inputWhite = value;
            }
        }

        public HistoRemap()
        {
            this.Reset();
        }

        public HistoRemap(string controlString)
        {
            string[] parts = controlString.Trim().Split(' ');

            this.Reset();

            this.InputBlack = Convert.ToInt32(parts[0]);
            this.InputGray = Convert.ToInt32(parts[1]);
            this.InputWhite = Convert.ToInt32(parts[2]);
            this.OutputBlack = Convert.ToInt32(parts[3]);
            this.OutputWhite = Convert.ToInt32(parts[4]);

            this.RemapAll();
        }

        public HistoRemap(double psGamma)
        {
            this.Reset();
            this.InputGray = GammaRemap.GetGrayFromGamma(1 / psGamma);
            this.RemapAll();
        }

        public HistoRemap(int inputBlack, int inputGray, int inputWhite, int outputBlack, int outputWhite)
        {
            this.Reset();

            this.InputBlack = inputBlack;
            this.InputGray = inputGray;
            this.InputWhite = inputWhite;
            this.OutputBlack = outputBlack;
            this.OutputWhite = outputWhite;

            this.RemapAll();
        }

        public void Reset()
        {
            this.inputBlack = 0;
            this.inputGray = 127;
            this.inputWhite = 255;
            this.outputBlack = 0;
            this.outputWhite = 255;

            this.InputBlackDiff = 0;
            this.InputGrayDiff = 0;
            this.InputWhiteDiff = 0;
            this.OutputBlackDiff = 0;
            this.OutputWhiteDiff = 0;
        }

        //provide indexer for external access of remap array
        //but it will be slow, so do everything in this class's methods
        public int this[int i]
        {
            get { return remap[i]; }
        }

        //compute remap array for input slider values
        //assume black<gray<white
        private void RemapInput(int black, int gray, int white)
        {
            for (int i = 0; i < black; i++)
                remap[i] = black;   //make anything less than black, black
            for (int i = white + 1; i <= 255; i++)
                remap[i] = white;   //make anything greater than white, white
        }


        //compute remap array for output slider values
        private void RemapOuput(int blackI, int whiteI, int blackO, int whiteO)
        {
            double slope = (double)(whiteO - blackO) / (whiteI - blackI);

            for (int i = 0; i <= 255; i++)
            {
                int newValue = (int)Math.Floor(slope * (remap[i] - blackI) + blackO);
                if (newValue < blackO)
                    newValue = blackO;
                if (newValue > whiteO)
                    newValue = whiteO;
                remap[i] = newValue;
            }
        }

        public void RemapAll(int inputBlack, int inputGray, int inputWhite, int outputBlack, int outputWhite)
        {
            this.InputBlack = inputBlack;
            this.InputGray = inputGray;
            this.InputWhite = inputWhite;
            this.OutputBlack = outputBlack;
            this.OutputWhite = outputWhite;

            this.RemapAll();
        }

        public void RemapAll()
        {
            for (int i = 0; i <= 255; i++)
                remap[i] = (byte)i;

            RemapInput(inputBlack, inputGray, inputWhite);
            GammaRemap g = new GammaRemap();
            g.GrayGamma = inputGray;
            g.GammaAdjustRemapArray(remap);
            RemapOuput(inputBlack, inputWhite, outputBlack, outputWhite);

            this.PshopGamma = g.PshopGamma;
        }

        //given an input source array from a Histo Class and
        //the corresponding ouput target array.
        //Use the level values in remap[] to map the input array into the output array
        public void RemapHistoArray(int[] ihArray, int[] ohArray)
        {
            for (int i = 0; i < 256; i++)
                ohArray[i] = 0;         //init output array
            //place input array counts for all levels into remapped levels in output array 
            for (int i = 0; i < 256; i++)
                ohArray[remap[i]] += ihArray[i];
        }

        //given an origRgbArray of unmodified pixel data, use the remap[] array
        //to modify it and place results in modRgbArray.
        public void RemapImageArray(byte[] origRgbArray, byte[] modRgbArray)
        {
            for (int i = 0; i < origRgbArray.Length; i += 4)
            {
                modRgbArray[i + (int)RGB.Red] = (byte)remap[origRgbArray[i + (int)RGB.Red]];
                modRgbArray[i + (int)RGB.Green] = (byte)remap[origRgbArray[i + (int)RGB.Green]];
                modRgbArray[i + (int)RGB.Blue] = (byte)remap[origRgbArray[i + (int)RGB.Blue]];
            }
        }

        public static bool operator ==(HistoRemap a, HistoRemap b)
        {
            if (System.Object.ReferenceEquals(a, b))
            {
                return true;
            }

            if (((object)a == null) || ((object)b == null))
            {
                return false;
            }

            // Return true if the fields match:
            return a.InputBlack == b.InputBlack
                && a.InputGray == b.InputGray
                && a.InputWhite == b.InputWhite
                && a.OutputBlack == b.OutputBlack
                && a.OutputWhite == b.OutputWhite;
        }

        public static bool operator !=(HistoRemap a, HistoRemap b)
        {
            return !(a == b);
        }

        public override string ToString()
        {
            return String.Format("I: {0} ({1}), {2} ({3}), {4} ({5}) O: {6} ({7}), {8} ({9})", this.InputBlack, this.InputBlackDiff
                , this.InputGray, this.InputGrayDiff
                , this.InputWhite, this.InputWhiteDiff
                , this.OutputBlack, this.OutputBlackDiff
                , this.OutputWhite, this.OutputWhiteDiff);
        }

        public override bool Equals(object obj)
        {
            return obj as HistoRemap == this;
        }

        public object Clone()
        {
            HistoRemap historyRemap = new HistoRemap();

            historyRemap.inputBlack = this.inputBlack;
            historyRemap.inputGray = this.inputGray;
            historyRemap.inputWhite = this.inputWhite;
            historyRemap.outputBlack = this.outputBlack;
            historyRemap.outputWhite = this.outputWhite;


            historyRemap.InputBlackDiff = this.InputBlackDiff;
            historyRemap.InputGrayDiff = this.InputGrayDiff;
            historyRemap.InputWhiteDiff = this.InputWhiteDiff;
            historyRemap.OutputBlackDiff = this.OutputBlackDiff;
            historyRemap.OutputWhiteDiff = this.OutputWhiteDiff;

            historyRemap.RemapAll();

            return historyRemap;
        }

        public override int GetHashCode()
        {
            return (this.inputBlack + this.inputGray + this.inputWhite + this.outputBlack + this.outputWhite);
        }
    }
}
