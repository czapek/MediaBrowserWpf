using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaBrowser4.Objects
{
    public struct AspectRatio
    {
        public double Ratio;
        public string Name;

        private static List<AspectRatio> aspectRatioList;
        public static List<AspectRatio> GetAspectRatioList()
        {
            if (aspectRatioList == null)
            {
                aspectRatioList = new List<AspectRatio>();
                aspectRatioList.Add(new AspectRatio() { Name = "16:9", Ratio = 16.0 / 9.0 });
                aspectRatioList.Add(new AspectRatio() { Name = "16:10", Ratio = 16.0 / 10.0 }); 
                aspectRatioList.Add(new AspectRatio() { Name = "4:3", Ratio = 4.0 / 3.0 });
                aspectRatioList.Add(new AspectRatio() { Name = "3:2", Ratio = 3.0 / 2.0 });
                aspectRatioList.Add(new AspectRatio() { Name = "3:1", Ratio = 3.0 / 1.0 });
                aspectRatioList.Add(new AspectRatio() { Name = "2:1", Ratio = 2.0 / 1.0 });
                aspectRatioList.Add(new AspectRatio() { Name = "1:1", Ratio = 1.0 / 1.0 });
            }
            return aspectRatioList;
        }

        public static AspectRatio GetNearest(double relation)
        {        
            double diff = double.MaxValue;
            AspectRatio aspectRatioResult = new AspectRatio();

            foreach (AspectRatio aspectRatio in GetAspectRatioList())
            {
                if (Math.Abs(relation - aspectRatio.Ratio) < diff)
                {
                    diff = Math.Abs(relation - aspectRatio.Ratio);
                    aspectRatioResult = aspectRatio;
                }
            }

            return aspectRatioResult;
        }

        public override string ToString()
        {
            return this.Name;
        }
    }
}
