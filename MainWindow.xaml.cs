using HandyControl;
using HandyControl.Controls;
using HandyControl.Data;
using HandyControl.Themes;
using HandyControl.Tools;
using Microsoft.Win32;
using SimpleFileLocker.Locker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
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
        bool ProcessWorking = false;
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
            string selected = mode_box.Text.ToLower();
            if (selected == "unlock")
            {
                protect_group.IsEnabled = true;
            }
            else if (selected == "lock")
            {
                protect_group.IsEnabled = false;
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
            }
        }
        
        private void Start_button_clicked(object sender, RoutedEventArgs e) //실행
        {
            if(ProcessWorking)
            {
                Growl.Warning(new GrowlInfo { Message = "이미 작업이 실행중입니다.", ShowDateTime = false });
                return;
            }
            string mode = mode_box.Text.ToLower();
            string path = dir_box.Text;
            string password = password_box.Password;
            string protect = CheckStatus();
            if (mode != "" && path != "" && protect != "")
            {
                int result = RustLib.simple_file(mode, path, password, protect);
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
            else
            {
                Growl.Warning(new GrowlInfo { Message = "설정 값이 부족합니다.", ShowDateTime = false });
            }
            
            ProcessWorking = false;
        }
        private void Help_button_clicked(object sender, RoutedEventArgs e)
        {
            Growl.Info(new GrowlInfo { Message = "1. Open버튼을 통해 파일을 선택\n2. 비번입력과 잠금/해제 기능 중 선택\n3. 보호기능 선택 & 실행", ShowDateTime = false });

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
    }
}
    
