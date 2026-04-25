using System;
using Eto.Forms;

namespace Unai.ExtendedBinaryWaterfall.Gui.EtoForms.Wpf
{
	class Program
	{
		[STAThread]
		public static void Main(string[] args)
		{
			new Application(Eto.Platforms.Wpf).Run(new MainForm());
		}
	}
}
