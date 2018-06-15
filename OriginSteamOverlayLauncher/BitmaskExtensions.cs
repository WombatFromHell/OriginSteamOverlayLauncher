using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Text;

namespace OriginSteamOverlayLauncher
{
    public static class BitmaskExtensions
    {
        private static bool IsCPUHyperthreaded(out int NumberOfPCores)
        {// use WMI to return a bool if Logical CPUs do not match CPU Cores
            try
            {
                int _pCores = 0;
                int _lCores = Environment.ProcessorCount;

                foreach (var item in new ManagementObjectSearcher("Select * from Win32_Processor").Get())
                {// accumulate from Win32_Processor count to get number of physical cores
                    _pCores += Int32.Parse(item["NumberOfCores"].ToString());
                }

                Program.Logger("OSOL", String.Format("Physical CPUs: {0} / Logical CPUs: {1}", _pCores, _lCores));
                NumberOfPCores = _pCores > 0 ? _pCores : 1; // share our physical core count externally

                if (_pCores != _lCores)
                {// if physical cores and logical cores differ we're hyperthreaded
                    return true;
                }
            }
            catch (ManagementException ex)
            {
                Program.Logger("EXCEPTION", ex.Message);
            }

            NumberOfPCores = 1;
            return false;
        }

        public static String AffinityToCoreString(UInt64 inputArray)
        {// return a string of numbered cores from a ulong affinity mask
            char[] _chr = Convert.ToString((long)inputArray, 2).ToCharArray();
            Array.Reverse(_chr);
            StringBuilder _str = new StringBuilder();

            if (_chr.Length > 0)
            {
                for (int i = 0; i < _chr.Length; i++)
                {
                    if ((_chr[i] & 1) != 0)
                    {// parse each flipped bit in the array
                        if (i != _chr.Length-1)
                            _str.Append(i + ","); // first or penultimate
                        else
                            _str.Append(i); // last bit
                    }
                }

                return _str.ToString();
            }

            return String.Empty;
        }

        public static bool TryParseCoreString(String inputString, out UInt64 result)
        {// take a string of digits delimited by commas and return a boolean along with a result
            result = 0;

            try
            {
                char[] _chr = !String.IsNullOrEmpty(inputString) ? inputString.Replace(",", "").ToCharArray() : null;
                long _result = 0;

                if (_chr.Length > 0)
                {
                    for (int i = 0; i <= _chr.Length-1; i++)
                    {
                        if (Char.IsDigit(_chr[i]))
                        {
                            int x = int.Parse(_chr[i].ToString());
                            _result = i == 0 ? (1 << x) : (1 << x) + _result;
                        }
                    }

                    if (_result > 0)
                    {
                        result = (ulong)_result;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                //Program.Logger("EXCEPTION", ex.Message);
                throw new Exception(ex.Message);
            }

            return false;
        }

        public static bool TryParseAffinity(string bitmask, out UInt64 result)
        {// convert bitmask string to ulong internally
            result = 0;
            bool _isHT = IsCPUHyperthreaded(out int _physCores);
            // for sanity cap our max input to 32 cores (0x1FFFFFFFF)
            ulong maxResult = 8589934591;

            // provide shortcuts for common affinity masks (be smart about HT)
            if (Settings.StringEquals(bitmask, "DualCore"))
            {// avoid CPU0 if possible
                result = _isHT ? (ulong)0xA : (ulong)0x3;
                return true;
            }
            else if (Settings.StringEquals(bitmask, "QuadCore"))
            {
                result = _isHT ? (ulong)0xAA : (ulong)0x15;
                return true;
            }
            else if (_isHT && Settings.StringEquals(bitmask, "DisableHT"))
            {
                long _aCores = 0;

                for (int i = 0; i <= _physCores * 2; i++)
                {// loop through and accumulate bits
                    _aCores += (1 << i);
                    i++; // .. for every other logical core
                }

                result = (UInt64)_aCores;
                return true;
            }
            else if (!String.IsNullOrEmpty(bitmask) && !Settings.StringEquals(bitmask, "DisableHT"))
            {// just convert what's there if possible
                // pass string along to TryParseCoreString first
                if (bitmask.Length > 1
                    && Program.OrdinalContains(",", bitmask) && TryParseCoreString(bitmask, out UInt64 _stringResult))
                {// try parsing as a core string - ints delimited by commas
                    Program.Logger("OSOL", String.Format("Parsed core mask: {0}", AffinityToCoreString(_stringResult)));
                    result = _stringResult; // copy our output externally
                }
                else if (bitmask.Length > 0
                    && !Program.OrdinalContains(",", bitmask) && UInt64.TryParse(bitmask, out result))
                {// attempt to parse using ulong conversion
                    Program.Logger("OSOL", String.Format("Parsed mumber mask to cores: {0}", AffinityToCoreString(result)));
                }
                else if (bitmask.Length > 1
                    && Program.OrdinalContains("0x", bitmask)
                    && UInt64.TryParse(bitmask.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result))
                {// last ditch attempt to parse using hex conversion (in ordinal/invariant mode)
                    Program.Logger("OSOL", String.Format("Parsed hex mask to cores: {0}", AffinityToCoreString(result)));
                }

                // sanity check our max return
                if (result > maxResult)
                    result = maxResult;

                return result != 0 ? true : false;
            }
            else if (!String.IsNullOrEmpty(bitmask))
            {// default to single core if nothing else is a valid parse
                result = (UInt64)1;
                return true;
            }

            return false;
        }
    }
}
