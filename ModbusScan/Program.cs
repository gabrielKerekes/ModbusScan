using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Modbus.Device;
using Mono.Options;

namespace ModbusScan
{
    // todo: refactor
    public class Program
    {
        public class Options
        {
            public string Ip { get; set; }
            public int TcpPort { get; set; }
            public string PortName { get; set; }
            public ModbusType ModbusType { get; set; }
            public int BaudRate { get; set; }
            public Parity Parity { get; set; }
            public int DataBits { get; set; }
            public StopBits StopBits { get; set; }
            public int Timeout { get; set; }
            public int RegisterNumber { get; set; }
            public ScanType ScanType { get; set; }
            public int Address { get; set; }
            public ModbusRegisterType RegisterType { get; set; }
            public bool Help { get; set; }

            public Options()
            {
                Ip = "192.168.0.1";
                TcpPort = 502;
                PortName = "COM4";
                ModbusType = ModbusType.Rtu;
                BaudRate = 19200;
                Parity = Parity.Even;
                DataBits = 8;
                StopBits = StopBits.One;
                Timeout = 100;
                RegisterNumber = 1;
                Address = 1;
                RegisterType = ModbusRegisterType.Holding;
                Help = false;

                ScanType = ScanType.Addresses;
            }

            public override string ToString()
            {
                var sb = new StringBuilder();

                sb.AppendLine($"Ip: {Ip}");
                sb.AppendLine($"TcpPort: {TcpPort}");
                sb.AppendLine($"PortName: {PortName}");
                sb.AppendLine($"ModbusType: {ModbusType}");
                sb.AppendLine($"BaudRate: {BaudRate}");
                sb.AppendLine($"Parity: {Parity}");
                sb.AppendLine($"DataBits: {DataBits}");
                sb.AppendLine($"StopBits: {StopBits}");
                sb.AppendLine($"Timeout: {Timeout}");
                sb.AppendLine($"RegisterNumber: {RegisterNumber}");
                sb.AppendLine($"Address: {Address}");
                sb.AppendLine($"RegisterType: {RegisterType}");
                sb.AppendLine($"Help: {Help}");
                sb.AppendLine($"ScanType: {ScanType}");

                return sb.ToString();
            }
        }

        public enum ModbusType
        {
            Rtu, Tcp,
        }

        public enum ScanType
        {
            Addresses, Registers, Ips,
        }

        public enum ModbusRegisterType
        {
            Holding, DiscreteInput, AnalogInput, Coil,
        }

        public static void Main(string[] args)
        {
            var options = new Options();

            var optionSet = new OptionSet
            {
                { "ip=", v => options.Ip = v },
                { "tcpPort", v => options.TcpPort = int.Parse(v) },
                { "port|p=", v => options.PortName = v },
                { "mdbs|m=", v => options.ModbusType = (ModbusType)Enum.Parse(typeof(ModbusType), v) },
                { "baud|b=", v => options.BaudRate = int.Parse(v) },
                { "parity|pa=", v => options.Parity = (Parity)Enum.Parse(typeof(Parity), v) },
                { "dataBits|d=", v => options.DataBits = int.Parse(v) },
                { "stopBits|s=", v => options.StopBits = (StopBits)Enum.Parse(typeof(StopBits), v) },
                { "timeout|t=", v => options.Timeout = int.Parse(v) },
                { "register|r=", v => options.RegisterNumber = int.Parse(v) },
                { "address|a=", v => options.Address = int.Parse(v) },
                { "scanType|sc=", v => options.ScanType = (ScanType)Enum.Parse(typeof(ScanType), v) },
                { "registerType|rt=", v => options.RegisterType = (ModbusRegisterType)Enum.Parse(typeof(ModbusRegisterType), v) },
                { "help|h", v =>  options.Help = true },
            };

            List<string> extra = optionSet.Parse(args);

            if (options.Help)
            {
                optionSet.WriteOptionDescriptions(Console.Out);
                return;
            }

            Console.WriteLine(options);
            Console.WriteLine("--------------------------------");
            Console.WriteLine();
            
            if (options.ScanType == ScanType.Addresses)
            {
                ScanDevices(options);
            }
            else if (options.ScanType == ScanType.Registers)
            {
                ScanSingleAddress(options);
            }
            else if (options.ScanType == ScanType.Ips)
            {
                ScanIps(options);
            }
        }

        private static SerialPort CreateAndOpenPort(Options options)
        {
            var serialPort = new SerialPort(options.PortName);
            serialPort.BaudRate = options.BaudRate;
            serialPort.Parity = options.Parity;
            serialPort.DataBits = options.DataBits;
            serialPort.StopBits = options.StopBits;
            serialPort.ReadTimeout = options.Timeout;
            serialPort.WriteTimeout = options.Timeout;
            serialPort.Open();

            return serialPort;
        }

        private static ModbusMaster CreateMaster(Options options)
        {
            if (options.ModbusType == ModbusType.Rtu)
            {
                return ModbusSerialMaster.CreateRtu(CreateAndOpenPort(options));
            }
            else if (options.ModbusType == ModbusType.Tcp)
            {
                var tcpClient = new TcpClient();
                var master = ModbusIpMaster.CreateIp(tcpClient);
                if (!tcpClient.ConnectAsync(options.Ip, options.TcpPort).Wait(100))
                {
                    throw new Exception("client connection error");
                }
                tcpClient.ReceiveTimeout = options.Timeout;
                tcpClient.SendTimeout = options.Timeout;
                return master;
            }

            return null;
        }

        private static void ScanIps(Options options)
        {
            var splitIp = options.Ip.Split('.');
            var success = false;
            for (var i = 0; i < 256; i++)
            {
                try
                {
                    var newIp = $"{splitIp[0]}.{splitIp[1]}.{splitIp[2]}.{i}";
                    options.Ip = newIp;
                    var modbusMaster = CreateMaster(options);
                    var response = TryRead(modbusMaster, options.RegisterType, i, options.RegisterNumber);
                    Console.WriteLine($"----Modbus master found on {newIp}---- {string.Join(",", response)}");
                    success = true;
                }
                catch (TimeoutException ex)
                {
                    Console.WriteLine($"Timeout on address {options.Ip} - {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error on address {options.Ip} - {ex.Message}");
                }

                Console.WriteLine($"------------------{success}------------------------");
                Console.WriteLine();
                Thread.Sleep(30);
            }
        }

        private static void ScanSingleAddress(Options options)
        {
            //var serialPort = CreateAndOpenPort(options);

            var success = false;
            for (var i = 0; i < 0xFFFF; i += 5)
            {
                try
                {
                    var modbusMaster = CreateMaster(options);
                    var response = TryRead(modbusMaster, options.RegisterType, options.Address, options.RegisterNumber);
                    Console.WriteLine($"----Valid response on register {i}----{string.Join(",", response)}");
                    success = true;
                }
                catch (TimeoutException ex)
                {
                    Console.WriteLine($"Timeout on register {i} - {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error on register {i} - {ex.Message}");
                }

                Console.WriteLine($"------------------{success}------------------------");
                Console.WriteLine();
                Thread.Sleep(30);
            }
        }

        private static ushort[] TryRead(ModbusMaster modbus, ModbusRegisterType registerType, int address, int register)
        {
            switch (registerType)
            {
                case ModbusRegisterType.AnalogInput:
                    return modbus.ReadInputRegisters((byte) address, (ushort) register, 1);
                case ModbusRegisterType.Holding:
                    return modbus.ReadHoldingRegisters((byte) address, (ushort) register, 1);
                case ModbusRegisterType.Coil:
                    return modbus.ReadCoils((byte) address, (ushort) register, 1).Select(c => c ? (ushort) 1 : (ushort) 0).ToArray();
                case ModbusRegisterType.DiscreteInput:
                    return modbus.ReadInputs((byte)address, (ushort)register, 1).Select(c => c ? (ushort) 1 : (ushort) 0).ToArray();
            }

            return null;
        }

        private static void ScanDevices(Options options)
        {
            //var serialPort = CreateAndOpenPort(options);

            var modbusMaster = CreateMaster(options);
            var success = false;
            for (var i = 0; i < 256; i++)
            {
                try
                {
                    ////Console.WriteLine(modbusSerialMaster.ReadHoldingRegisters((byte) i, 1, 1)[0]);
                    ////serialPort.Write(new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00}, 0, 7);
                    //var data = new byte[] { (byte) i, 0x03, 0x00, 0x01, 0x00, 0x01 };

                    ////var crc = CRC.ComputeChecksumBytes(data);
                    //var crc = CRC.CalculateCrc(data);


                    //var z = new byte[data.Length + crc.Length];
                    //data.CopyTo(z, 0);
                    //crc.CopyTo(z, data.Length);
                    //serialPort.Write(z, 0, z.Length);
                    //Thread.Sleep(100);
                    //var readBytes = new byte[100];
                    //string readString;
                    ////Console.WriteLine(serialPort.ReadLine());
                    //if (serialPort.BytesToRead > 0)
                    //{
                    //    readString = serialPort.ReadExisting();

                    //    Console.WriteLine($"----Modbus master found on {i}---- {string.Join(",", Encoding.ASCII.GetBytes(readString))}");
                    //}
                    //else
                    //    Console.WriteLine($"Timeout on address {i}");
                    var response = TryRead(modbusMaster, options.RegisterType, i, options.RegisterNumber);
                    Console.WriteLine($"----Modbus master found on {i}---- {string.Join(",", response)}");
                    success = true;
                }
                catch (TimeoutException ex)
                {
                    Console.WriteLine($"Timeout on address {i} - {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error on address {i} - {ex.Message}");
                }

                Console.WriteLine($"------------------{success}------------------------");
                Console.WriteLine();
                Thread.Sleep(30);
            }
        }
    }
}
