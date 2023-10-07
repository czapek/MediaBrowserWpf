using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;

namespace MediaBrowser4.Objects
{
    public class Layer
    {
        internal int position;
        internal string action, edit;
        public int Position
        {
            get { return position; }
        }

        public string Edit
        {
            get { return edit; }
        }

        public string Action
        {
            get { return action; }
            set
            {
                action = value;
            }
        }

        public string ActionGui
        {
            get
            {
                string[] split;

                switch (this.Edit)
                {
                    case "TRIM":
                        split = this.Action.Split(' ');
                        double a = Convert.ToDouble(split[0], CultureInfo.InvariantCulture.NumberFormat);
                        double b = Convert.ToDouble(split[1], CultureInfo.InvariantCulture.NumberFormat);

                        return (a > 0 ? "start " + MediaBrowser4.Utilities.DateAndTime.FormatVideoTime(a) : String.Empty)
                            + (a > 0 && b > 0 ? ", " : String.Empty)
                            + (b > 0 ? "stop " + MediaBrowser4.Utilities.DateAndTime.FormatVideoTime(b) : String.Empty);

                    case "FLIP":
                        split = this.Action.Split(' ');
                        return String.Format("{0} {1}",
                            Convert.ToInt32(split[0], CultureInfo.InvariantCulture.NumberFormat) > 0 ? "horizontal" : "",
                            Convert.ToInt32(split[1], CultureInfo.InvariantCulture.NumberFormat) > 0 ? "vertical" : "");

                    case "ROT":
                        return String.Format("{0:n1}°", Convert.ToDouble(this.Action, CultureInfo.InvariantCulture.NumberFormat));

                    case "ROTC":
                        return String.Format("{0:n1}°", Convert.ToDouble(this.Action, CultureInfo.InvariantCulture.NumberFormat));

                    case "CROP":
                    case "CLIP":
                        split = this.Action.Split(' ');
                        return String.Format("{0}%, {1}%, {2}%, {3}%",
                         Convert.ToDouble(split[0], CultureInfo.InvariantCulture.NumberFormat),
                         Convert.ToDouble(split[1], CultureInfo.InvariantCulture.NumberFormat),
                         Convert.ToDouble(split[2], CultureInfo.InvariantCulture.NumberFormat),
                         Convert.ToDouble(split[3], CultureInfo.InvariantCulture.NumberFormat));

                    case "ZOOM":
                        split = this.Action.Split(' ');
                        return String.Format("X {0}, Y {1}%, {2}x, {3}",
                         Convert.ToDouble(split[0], CultureInfo.InvariantCulture.NumberFormat),
                         Convert.ToDouble(split[1], CultureInfo.InvariantCulture.NumberFormat),
                         Convert.ToDouble(split[2], CultureInfo.InvariantCulture.NumberFormat),
                         Convert.ToDouble(split[3], CultureInfo.InvariantCulture.NumberFormat));

                    case "LEVELS":
                        split = this.Action.Split('-');
                        if (split[0].Trim() == split[1].Trim() && split[1].Trim() == split[2].Trim())
                        {
                            return String.Format("{0}", split[0].Trim());
                        }
                        else
                        {
                            return String.Format("R {0}, G {1}, B {2}", split[0].Trim(), split[1].Trim(), split[2].Trim());
                        }

                    case "AVSY":
                        string result = this.Action.Length > 40 ? this.Action.Substring(0, 36) + " ..." : action;
                        result = result.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
                        return result;

                    default:
                        return this.Action;
                }
            }
        }

        public string EditGui
        {
            get
            {

                switch (this.Edit)
                {
                    case "LEVELS":
                        return "Gamma";
                    case "TRIM":
                        return "Beschneiden";
                    case "CROP":
                        return "Zuschneiden";
                    case "CLIP":
                        return "Passepartout";
                    case "ZOOM":
                        return "Größe";
                    case "GAMM":
                        return "Gamma (alt)";
                    case "CONT":
                        return "Kontrast (alt)";
                    case "ROTC":
                        return "Zuschneiden (alt)";
                    case "ROT":
                        return "Drehen";
                    case "FLIP":
                        return "Spiegeln";
                    case "AVSY":
                        return "AviSynth";
                    case "MPLY":
                        return "Multiplayer";
                    default:
                        return string.Empty;
                }
            }

        }

        public static Layer GetDefaultLayer(string edit, int pos)
        {
            switch (edit)
            {
                case "LEVELS":
                    return new Layer(edit, "0 127 255 0 255 - 0 127 255 0 255 - 0 127 255 0 255", pos);
                case "FLIP":
                    return new Layer(edit, "0 0", pos);
                case "TRIM":
                    return new Layer(edit, "0.0 0.0", pos);
                case "CROP":
                    return new Layer(edit, "0.0 0.0 0.0 0.0", pos);
                case "CLIP":
                    return new Layer(edit, "0.0 0.0 0.0 0.0", pos);
                case "ZOOM":
                    return new Layer(edit, "0.0 0.0 0.0 0.0", pos);
                case "GAMM":
                    return new Layer(edit, "1.0 1.0 1.0", pos);
                case "CONT":
                    return new Layer(edit, "0", pos);
                case "ROTC":
                    return new Layer(edit, "0.0", pos);
                case "ROT":
                    return new Layer(edit, "0.0", pos);
                case "AVSY":
                    return new Layer(edit, "", pos);
                case "MPLY":
                    return new Layer(edit, "", pos);
                case "PANO":
                    return new Layer(edit, "", pos);
                case "MAINFOCUS":
                    return new Layer(edit, "0.0 0.0", pos);
                default:
                    return null;
            }
        }

        public override string ToString()
        {
            return this.EditGui + ": " + this.ActionGui;
        }

        public override int GetHashCode()
        {
            return this.position;
        }

        public override bool Equals(object obj)
        {
            return obj.GetHashCode() == this.GetHashCode();
        }

        public Layer(string edit, string action, int position)
        {
            this.position = position;
            this.action = action;
            this.edit = edit;
        }
    }
}
