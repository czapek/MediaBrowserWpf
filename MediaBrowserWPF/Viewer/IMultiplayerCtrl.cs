using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace MediaBrowserWPF.Viewer
{
    interface IMultiplayerCtrl
    {
        event EventHandler<MouseButtonEventArgs> MouseDoubleClick;
        event EventHandler<MouseButtonEventArgs> MouseDown;
        event EventHandler MediaLoaded;
        List<ViewerBaseControl> ViewerBaseControlList { get; }
    }
}
