using System;
using System.Diagnostics;
using System.IO;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using System.Security.Principal;

namespace rueso_helper
{
    internal class Program
    {
        const int MEM_COMMIT = 0x00001000;
        const int PAGE_READWRITE = 0x04;
        const int TYPE = 0x020000;


        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint dwSize,
            out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize,
            out int lpNumberOfBytesWritten);

        [DllImport("Kernel32.dll", SetLastError = true)]
        public static extern UIntPtr VirtualQueryEx         // сообщает информацию о памяти в другом процессе
                    (
                        IntPtr hProcess,                    // Дескриптора процесса
                        IntPtr pvAddress,                   // адрес виртуальной памяти
                        out MEMORY_BASIC_INFORMATION pmbi,  // это адрес структуры MEMORY_BASIC_INFORMATION,
            // которую надо создать перед вызовом функции
                        int dwLength                        // задает размер структуры MEMORY_BASIC_INFORMATION
                    );



        public struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;      // Сообщает то же значение, что и параметр pvAddress,
            // но округленное до ближайшего меньшего адреса, кратного размеру страницы
            public IntPtr AllocationBase;   // Идентифицирует базовый адрес региона, включающего в себя адрес,
            // указанный в параметре pvAddress
            public int AllocationProtect;   // Идентифицирует атрибут защиты, присвоенный региону при его резервировании
            public IntPtr RegionSize;       // Сообщаем суммарный размер (в байтах) группы
            public int State;               // Сообщает состояние (MEM_FRFF, MFM_RFSFRVE или MEM_COMMIT) всех смежных страниц
            public int Protect;             // Идентифицирует атрибут защиты (PAGE *) всех смежных страниц
            public int Type;                // Идентифицирует тип физической памяти (MEM_IMAGE, MEM_MAPPED или MEM PRIVATE)
        }


        private static void proc_Exited(object sender, EventArgs e)
        {
            Environment.Exit(1);
        }

        private static string Filename()
        {
            if (File.Exists(@"Bethesda.net_Launcher.exe")) return @"Bethesda.net_Launcher.exe";

            RegistryKey rk = Registry.LocalMachine.OpenSubKey("Software\\Zenimax_Online\\Launcher");

            try
            {
                if (File.Exists((string) (rk.GetValue("InstallPath")) + @"\Launcher\Bethesda.net_Launcher.exe"))
                    return (string) (rk.GetValue("InstallPath")) + @"\Launcher\Bethesda.net_Launcher.exe";
            }
            catch
            {
            }

            rk = Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\Zenimax_Online\\Launcher");
            try
            {
                if (File.Exists((string) (rk.GetValue("InstallPath")) + @"\Launcher\Bethesda.net_Launcher.exe"))
                    return (string) (rk.GetValue("InstallPath")) + @"\Launcher\Bethesda.net_Launcher.exe";
            }
            catch
            {
            }

            if (File.Exists(@"C:\Program Files (x86)\Zenimax Online\Launcher\Bethesda.net_Launcher.exe"))
                return @"C:\Program Files (x86)\Zenimax Online\Launcher\Bethesda.net_Launcher.exe";
            if (File.Exists(@"C:\Program Files\Zenimax Online\Launcher\Bethesda.net_Launcher.exe"))
                return @"C:\Program Files\Zenimax Online\Launcher\Bethesda.net_Launcher.exe";

            return "0";
        }

        private static Process GetProcess()
        {
            Process[] localByName = Process.GetProcessesByName("Bethesda.net_Launcher");

            if (localByName.Length > 0)
            {
                return localByName[0];
            }
            Thread.Sleep(500);
            return GetProcess();
        }

        private static void Main(string[] args)
        {
            WindowsPrincipal pricipal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            bool hasAdministrativeRight = pricipal.IsInRole(WindowsBuiltInRole.Administrator);

            if (hasAdministrativeRight == false)
            {
                ProcessStartInfo processInfo = new ProcessStartInfo(); //создаем новый процесс
                processInfo.Verb = "runas"; //в данном случае указываем, что процесс должен быть запущен с правами администратора
                processInfo.FileName = System.Reflection.Assembly.GetExecutingAssembly().CodeBase; //указываем исполняемый файл (программу) для запуска
                try
                {
                    Process.Start(processInfo); //пытаемся запустить процесс
                }
                catch
                {
                    //Ничего не делаем, потому что пользователь, возможно, нажал кнопку "Нет" в ответ на вопрос о запуске программы в окне предупреждения UAC (для Windows 7)
                }
                Environment.Exit(1);
            }


            bool isSteam = false;

            RegistryKey rk = Registry.LocalMachine.OpenSubKey("Software\\Zenimax_Online\\Launcher");

            try
            {
                if (File.Exists((string) (rk.GetValue("InstallPath")) + @"\installScript.vdf"))
                {
                    isSteam = true;
                }
            }
            catch
            {
            }
            ;

            rk = Registry.LocalMachine.OpenSubKey("Software\\Wow6432Node\\Zenimax_Online\\Launcher");

            try
            {
                if (File.Exists((string) (rk.GetValue("InstallPath")) + @"\installScript.vdf"))
                {
                    isSteam = true;
                }
            }
            catch
            {
            }
            ;


            var proc = new Process();

            if (isSteam)
            {
                Process.Start("steam://run/306130");
                proc = GetProcess();
                proc.EnableRaisingEvents = true;
                proc.Exited += proc_Exited;
            }
            else
            {
                proc.StartInfo.FileName = Filename();
                proc.EnableRaisingEvents = true;
                proc.Exited += proc_Exited;

                try
                {
                    proc.Start();
                }
                catch
                {
                    Environment.Exit(1);
                }
            }

            if (proc.Id == 0)
            {
                return;
            }
            IntPtr processHandle = OpenProcess(0x001F0FFF, false, proc.Id);

            Thread.Sleep(1000);
            MemorySearch(processHandle);
        }

        private static void MemorySearch(IntPtr processHandle)
        {
            long ptr1Count = 0x00000000; // адрес после недоступной зоны
            MEMORY_BASIC_INFORMATION b = new MEMORY_BASIC_INFORMATION();
            // Объявляем структуру
            IntPtr ptr1 = new IntPtr(ptr1Count);
            bool isFound = false;
            long offset;

            while (ptr1Count <= 0x7FFE0000) // До конца виртуальной памяти для данного процесса
            {
                VirtualQueryEx(processHandle, ptr1, out b, Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION)));

                if (b.Protect == PAGE_READWRITE && b.State == MEM_COMMIT)
                {
                    byte[] buffer = new byte[(uint)b.RegionSize];
                    int ret;

                    ReadProcessMemory(processHandle, b.BaseAddress, buffer, (uint)b.RegionSize, out ret);
                    for (uint j = 0; j <= (uint) b.RegionSize - 11; j++)
                    {
                        if (buffer[j] == 0x4C &&
                            buffer[j + 1] == 0x61 &&
                            buffer[j + 7] == 0x65 &&
                            buffer[j + 8] == 0x2E &&
                            buffer[j + 9] == 0x32 &&
                            buffer[j + 10] == 0x3d)
                        {
                            offset = (int)b.BaseAddress + j;

                            byte[] ru = Encoding.ASCII.GetBytes("ru");
                            WriteProcessMemory(processHandle, (IntPtr) (offset + 11), ru, ru.Length, out ret);
                            isFound = true;
                            j = (uint) b.RegionSize;
                        }
                    }
                }

                if (isFound)
                {
                    break;
                }
                ptr1Count = ptr1Count + (int)b.RegionSize;
                ptr1 = (IntPtr)ptr1Count;
            }

            if (isFound == false)
            {
                Thread.Sleep(1000);
                MemorySearch(processHandle);
            }
            else
            {
                Environment.Exit(1);
            }
        }
    }
}