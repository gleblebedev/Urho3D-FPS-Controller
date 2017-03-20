using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Urho;

namespace Urho3D_FPS_Controller
{
    public static class Program
    {
        /// <summary>
        ///     The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                var options = new ApplicationOptions("Data")
                {
                    WindowedMode = Debugger.IsAttached,
                    LimitFps = true,
                    //TouchEmulation = true
                };
                var _app = new GamePlayer(options);
                _app.Run();
            }
            catch (BadImageFormatException exception)
            {
                MessageBox.Show(exception.ToString(), "Can't load file " + exception.FileName, MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
