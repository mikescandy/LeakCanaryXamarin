using Android.App;
using Android.OS;
using System.Threading.Tasks;
using System.Threading;

namespace Sample
{
    [Activity(Label = "Sample", MainLauncher = true, Icon = "@mipmap/icon")]
    public class MainActivity : Activity
    {


        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);
            var button = FindViewById(Resource.Id.async_task);
            button.Click += async (sender, e) =>
            {               
                await Task.Factory.StartNew(() => Thread.Sleep(5000));
            };
        }


    }
}

