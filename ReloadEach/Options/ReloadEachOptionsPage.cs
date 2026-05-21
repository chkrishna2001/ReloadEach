using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace ReloadEach.Options
{
    public class ReloadEachOptionsPage : DialogPage
    {
        private int delaySeconds = 2;

        [Category("General")]
        [DisplayName("Delay seconds")]
        [Description("Delay in seconds between reload of one project and moving to the next.")]
        public int DelaySeconds
        {
            get => delaySeconds;
            set => delaySeconds = value < 0 ? 0 : value;
        }
    }
}
