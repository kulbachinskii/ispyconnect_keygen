using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace keygen
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var Dll_File = @"C:\Program Files\Agent\CoreLogic.dll";
                var Dll = File.ReadAllBytes(Dll_File);

                var Pattern_Orig = new byte[]    { 0x0A, 0x2C, 0x2F, 0x28, 0x0E, 0x02, 0x00, 0x0A, 0x80, 0x46 };
                var Pattern_Patched = new byte[] { 0x0A, 0x2D, 0x2F, 0x28, 0x0E, 0x02, 0x00, 0x0A, 0x80, 0x46 };
                var FoundOffsets_Orig = Locate(Dll, Pattern_Orig);
                var FoundOffsets_Patched = Locate(Dll, Pattern_Patched);

                if (FoundOffsets_Orig.Length == 0 && FoundOffsets_Patched.Length == 1)
                {
                    Log("DLL already patched.");
                }
                else if (FoundOffsets_Orig.Length == 1 && FoundOffsets_Patched.Length == 0)
                {
                    Log("Patching DLL...");
                    Dll[FoundOffsets_Orig[0] + 1]++;
                    File.Move(Dll_File, Dll_File + "." + DateTime.UtcNow.Ticks);
                    File.WriteAllBytes(Dll_File, Dll);

                }
                else throw new Exception("Unsupported DLL version");

                var Config_File = @"C:\Program Files\Agent\Media\XML\config.xml";
                var Config = File.ReadAllLines(Config_File, Encoding.UTF8);

                var UniqueID = "";
                foreach (var line in  Config)
                {
                    if (line.Contains("<UniqueID>"))
                    {
                        UniqueID = line.Replace("<UniqueID>", "").Replace("</UniqueID>", "").TrimStart().TrimEnd();
                        break;
                    }
                }
                if (string.IsNullOrEmpty(UniqueID))
                {
                    Log("Enter UniqueID:");
                    UniqueID = Console.ReadLine();
                }

                Log("Enter email:");
                var email = Console.ReadLine();
                var license_json = "{\"uniqueid\":\"" + UniqueID + "\",\"email\":\""+email+"\"}";
                var license_enc = EncryptData(license_json, UniqueID.Trim().ToUpperInvariant() + appendToken);

                for (int x = 0; x < Config.Length; x++)
                {
                    if (Config[x].Contains("<License"))
                    {
                        Config[x] = "<License>" + license_enc + "</License>";
                        break;
                    }
                }

                Log("Stopping Agent DVR...");
                Process.Start("net.exe", "stop Agent").WaitForExit(); 

                Log("Writing License to config...");
                File.Move(Config_File, Config_File + "." + DateTime.UtcNow.Ticks);
                File.WriteAllLines(Config_File, Config, Encoding.UTF8);

                Log("Starting Agent DVR...");
                Process.Start("net.exe", "start Agent").WaitForExit();

                Log("OK.");
            }
            catch (Exception ex)
            {
                Log("Error: " + ex.Message);
            }
            Log("Press any key to exit...");
            Console.ReadKey();
        }


        public static string appendToken = "_kbuytgkycfk";
        public static byte[] EncryptData(byte[] data, string password, PaddingMode paddingMode)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentNullException("data");
            }
            if (password == null)
            {
                throw new ArgumentNullException("password");
            }
            PasswordDeriveBytes passwordDeriveBytes = new PasswordDeriveBytes(password, Encoding.UTF8.GetBytes("Salt"));
            ICryptoTransform cryptoTransform = new RijndaelManaged
            {
                Padding = paddingMode
            }.CreateEncryptor(passwordDeriveBytes.GetBytes(16), passwordDeriveBytes.GetBytes(16));
            byte[] array;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (CryptoStream cryptoStream = new CryptoStream(memoryStream, cryptoTransform, CryptoStreamMode.Write))
                {
                    cryptoStream.Write(data, 0, data.Length);
                    cryptoStream.FlushFinalBlock();
                    array = memoryStream.ToArray();
                }
            }
            return array;
        }

        public static string EncryptData(string data, string password)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }
            return Convert.ToBase64String(EncryptData(Encoding.UTF8.GetBytes(data), password, PaddingMode.ISO10126));
        }




        static readonly int[] Empty = new int[0];
        public static int[] Locate(byte[] self, byte[] candidate)
        {
            var list = new List<int>();

            for (int i = 0; i < self.Length; i++)
            {
                if (!IsMatch(self, i, candidate))
                    continue;

                list.Add(i);
            }

            return list.Count == 0 ? Empty : list.ToArray();
        }

        static bool IsMatch(byte[] array, int position, byte[] candidate)
        {
            if (candidate.Length > (array.Length - position))
                return false;

            for (int i = 0; i < candidate.Length; i++)
                if (array[position + i] != candidate[i])
                    return false;

            return true;
        }



        static void Log(string msg)
        {
            Console.WriteLine(msg);
        }

    }
}
