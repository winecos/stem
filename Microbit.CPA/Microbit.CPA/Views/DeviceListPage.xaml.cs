﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace Microbit.CPA.Views
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class DeviceListPage : ContentPage
	{
		public DeviceListPage ()
		{
			InitializeComponent ();
		}
	}
}