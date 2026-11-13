using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace MiCaminoBJJ
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        // Equivale a --bg-primary (#171717) de wwwroot/app.css: la franja que queda
        // reservada arriba/abajo debe continuar el mismo color para que no se vea un
        // bloque distinto entre el sistema y la app.
        private const string AppBackgroundColor = "#171717";

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Android 15+ (API 35+) fuerza edge-to-edge al apuntar a SDK 35 o superior:
            // el contenido de la ventana se dibuja por detrás de la barra de estado y
            // del recorte de la cámara, por lo que el header del BlazorWebView quedaba
            // tapado por el reloj y los iconos del sistema.
            //
            // Aquí se reserva ese espacio UNA sola vez, sobre el contenedor nativo,
            // para que navbar y contenido arranquen por debajo del área reservada:
            // el valor no es un margen fijo, proviene de los insets reales de la
            // ventana (system bars + display cutout). En Android, el HTML/CSS no suma
            // ningún espacio superior (.status-bar-safe-area está oculto y no existe
            // una variable --safe-top en uso), así que el inset NO se aplica dos veces.
            //
            // En versiones anteriores a API 35 el sistema ya reserva esos insets
            // (sin edge-to-edge), por lo que no hacemos nada para no duplicarlos.
            if (Build.VERSION.SdkInt < BuildVersionCodes.VanillaIceCream)
                return;

            var content = FindViewById(Android.Resource.Id.Content);
            if (content is null)
                return;

            content.SetBackgroundColor(Android.Graphics.Color.ParseColor(AppBackgroundColor));
            content.SetOnApplyWindowInsetsListener(new SystemBarInsetsListener());
            content.RequestApplyInsets();
        }

        private sealed class SystemBarInsetsListener : Java.Lang.Object, global::Android.Views.View.IOnApplyWindowInsetsListener
        {
            public WindowInsets OnApplyWindowInsets(global::Android.Views.View view, WindowInsets insets)
            {
                // SystemBars = status bar (arriba) + navegación (abajo);
                // DisplayCutout = área del recorte de la cámara, cuando sobresale.
                var reserved = insets.GetInsets(
                    WindowInsets.Type.SystemBars() | WindowInsets.Type.DisplayCutout());

                view.SetPadding(reserved.Left, reserved.Top, reserved.Right, reserved.Bottom);

                // No consumimos los insets: el resto de la jerarquía (BlazorWebView)
                // sigue recibiéndolos normalmente.
                return insets;
            }
        }
    }
}
