using HandyControl;
using HandyControl.Controls;
using HandyControl.Data;
using HandyControl.Themes;
using HandyControl.Tools;
using Microsoft.Win32;
using SimpleFileLocker.Locker;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Window = HandyControl.Controls.Window;

namespace SimpleFileLocker
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public bool ProcessWorking = false; //작동중인지
        public bool IsNTFS = false; //선택한 파일이 NTFS시스템 파일인지
        

        public MainWindow()
        {
            
            InitializeComponent();
            add_item_on_mode_box();
            
        }

        public void add_item_on_mode_box()
        {
            mode_box.Items.Add("Lock");
            mode_box.Items.Add("Unlock");
            mode_box.SelectedIndex = 0;
        }
        public void mode_box_changed(object sender, EventArgs e)
        {
            string selected = mode_box.SelectedItem.ToString().ToLower();
            if (selected == "unlock")
            {
                protect_group.IsEnabled = false;
                // 안내를 위해 기본값을 유지하거나 특정 상태로 고정할 수 있습니다.
            }
            // 2. Lock 모드: NTFS 시스템일 때만 활성화하여 선택권 부여
            else if (selected == "lock")
            {
                if (IsNTFS)
                {
                    protect_group.IsEnabled = true;
                }
                else
                {
                    protect_group.IsEnabled = false;
                    protect_off.IsChecked = true; // NTFS가 아니면 강제로 Off
                }
            }
        }
        private void Open_button_clicked(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            // 2. 옵션 설정 (선택 사항)
            openFileDialog.Title = "파일을 선택해 주세요"; // 창 제목
            openFileDialog.Filter = "텍스트 파일 (*.txt)|*.txt*.*|모든 파일 (*.*)|"; // 확장자 필터
            openFileDialog.InitialDirectory = @"C:\"; // 초기 열리는 폴더 경로

            // 3. 창 띄우고 결과 받기
            // ShowDialog()가 true를 반환하면 사용자가 파일을 선택하고 '열기'를 누른 것입니다.
            if (openFileDialog.ShowDialog() == true)
            {
                dir_box.Text = openFileDialog.FileName;
                CheckDrive(dir_box.Text);
            }
        }
        
        private async void Start_button_clicked(object sender, RoutedEventArgs e) //실행
        {
            if(ProcessWorking)
            {
                return;
            }
            string mode = mode_box.Text.ToLower();

            string path = dir_box.Text;
            byte[] pathBytes = Encoding.UTF8.GetBytes(path + "\0");

            string password = password_box.Password;
            string protect = CheckStatus();
            if (mode != "" && path != "" && protect != "")
            {
                ProcessWorking = true;
                start_button.IsEnabled = false;
                try
                {
                    int result = await Task.Run(() => RustLib.simple_file(mode, pathBytes, password, protect));
                    switch (result)
                    {
                        case 0:
                            Growl.Success(new GrowlInfo { Message = "성공적으로 완료했습니다.", ShowDateTime = false });
                            break;
                        case 1:
                            Growl.Error(new GrowlInfo { Message = "암호화에 실패했습니다.", ShowDateTime = false });
                            break;
                        case 2:
                            Growl.Error(new GrowlInfo { Message = "파일 읽기에 실패했습니다.", ShowDateTime = false });
                            break;
                        case 3:
                            Growl.Error(new GrowlInfo { Message = "복호화에 실패했습니다.", ShowDateTime = false });
                            break;
                        case 4:
                            Growl.Warning(new GrowlInfo { Message = "이미 적용된 파일입니다.", ShowDateTime = false });
                            break;
                        default:
                            Growl.Fatal(new GrowlInfo { Message = "기타오류가 발생했습니다.", ShowDateTime = false });
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Growl.Fatal(new GrowlInfo { Message = "작업오류가 발생했습니다.", ShowDateTime = false });
                    //int result = RustLib.simple_file(mode, path, password, protect);
                }
                finally
                {
                    ProcessWorking = false;
                    start_button.IsEnabled = true;
                }
            }
            else
            {
                Growl.Warning(new GrowlInfo { Message = "설정 값이 부족합니다.", ShowDateTime = false });
                start_button.IsEnabled = true;
                ProcessWorking = false;
            }
        }
        private void Help_button_clicked(object sender, RoutedEventArgs e)
        {
            string msi2 = "데이터 은닉기능은 NTFS파일 시스템에서만 작동합니다.\n클라우드나 USB이동시에는 파일 데이터가 삭제될 수 있습니다.";
            Growl.Warning(new GrowlInfo { Message = msi2, ShowDateTime = false });
            string msi = "1. Open버튼을 통해 파일을 선택\n2. 비번입력과 잠금/해제 기능 중 선택\n3. 데이터 은닉기능 선택 & 실행";
            Growl.Info(new GrowlInfo { Message = msi, ShowDateTime = false });
        }
        private string CheckStatus()
        {
            if (protect_on.IsChecked == true)
            {
                return "on";
            }
            else if (protect_off.IsChecked == true)
            {
                return "off"; 
            }
            else
            {
                return "off";
            }
        }
        private void CheckDrive(string path)
        {
            if (path.StartsWith(@"\\"))
            {
                string msi = "네트워크 경로는 지원하지 않습니다.";
                Growl.Warning(new GrowlInfo { Message = msi, ShowDateTime = false });
                dir_box.Text = "";
                return;
            }
            try
            {
                string driveRoot = System.IO.Path.GetPathRoot(path);
                if (string.IsNullOrEmpty(driveRoot))
                {
                    string msi = "유효하지 않은 파일 경로입니다.";
                    Growl.Warning(new GrowlInfo { Message = msi, ShowDateTime = false });
                    dir_box.Text = "";
                    return;
                }
                DriveInfo drive = new DriveInfo(driveRoot);
                if (drive.IsReady)
                {
                    string drive_format = drive.DriveFormat;
                    if (drive.DriveType == DriveType.Network)
                    {
                        string msi = $"네트워크 드라이브를 지원하지 않습니다.\n드라이브 타입 : {drive_format}";
                        Growl.Warning(new GrowlInfo { Message = msi, ShowDateTime = false });
                        dir_box.Text = "";
                        return;
                    }
                    IsNTFS = drive.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase);
                    if(IsNTFS) //투명화 자동적용
                    {
                        protect_on.IsChecked = true;
                        if (mode_box.Text.Equals("lock", StringComparison.OrdinalIgnoreCase)) //락에 놓고 선택시
                        {
                            protect_group.IsEnabled = true;
                        }
                    }
                    else //Fat32등 다른 포맷시 버튼 비활성화 & 글씨 보이게
                    {
                        protect_group.IsEnabled = false;
                        protect_off.IsChecked = true;
                        string msi = $"NTFS 시스템이 아니므로 데이터 은닉 사용불가\n파일 형식 : {drive.DriveFormat}";
                        Growl.Warning(new GrowlInfo { Message = msi, ShowDateTime = false });
                    }
                }
            }
            catch (Exception ex)
            {
                dir_box.Text = "";
                string msi = $"드라이브 확인 중 오류\n{ex.Message}";
                Growl.Warning(new GrowlInfo { Message = msi, ShowDateTime = false });
            }
         }
       
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (ProcessWorking)
            {
                string msi = "작업이 진행 중입니다.";
                Growl.Warning(new GrowlInfo { Message = msi, ShowDateTime = false });
                e.Cancel = true;
            }
        }
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Environment.Exit(0);
        }
    }
}
    
