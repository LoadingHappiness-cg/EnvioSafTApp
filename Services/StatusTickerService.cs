using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EnvioSafTApp.Services
{
    public enum TickerMessageType
    {
        Success,
        Error,
        Warning
    }

    public class StatusTickerService
    {
        private readonly TextBlock _messageBlock;
        private readonly TextBlock _iconBlock;
        private readonly Border _backgroundBorder;

        public StatusTickerService(TextBlock messageBlock, TextBlock iconBlock, Border backgroundBorder)
        {
            _messageBlock = messageBlock;
            _iconBlock = iconBlock;
            _backgroundBorder = backgroundBorder;
        }

        public void ShowMessage(string message, TickerMessageType type)
        {
            _messageBlock.Text = message;

            // Define aparência com base no tipo de mensagem
            Brush background;
            Brush foreground;
            string icon;

            switch (type)
            {
                case TickerMessageType.Error:
                    background = new SolidColorBrush(Color.FromRgb(255, 235, 230)); // Vermelho claro
                    foreground = Brushes.DarkRed;
                    icon = "❌";
                    break;
                case TickerMessageType.Warning:
                    background = new SolidColorBrush(Color.FromRgb(255, 249, 196)); // Amarelo claro
                    foreground = Brushes.DarkOrange;
                    icon = "⚠️";
                    break;
                default:
                    background = new SolidColorBrush(Color.FromRgb(223, 246, 221)); // Verde claro
                    foreground = Brushes.Green;
                    icon = "✅";
                    break;
            }

            // Aplica visual
            _backgroundBorder.Background = background;
            _messageBlock.Foreground = foreground;
            _iconBlock.Text = icon;
            _iconBlock.Foreground = foreground;

            // Inicia a animação
            _backgroundBorder.BeginStoryboard(
                (Storyboard)Application.Current.FindResource("ShowTickerAnimation"));
        }
    }
}