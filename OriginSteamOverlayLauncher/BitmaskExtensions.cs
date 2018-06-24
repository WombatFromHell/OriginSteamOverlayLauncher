using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Text;

namespace OriginSteamOverlayLauncher
{
    public class BitmaskExtensions
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
#if DEBUG
                //throw new Exception(ex.Message);
#endif
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
        {// take a string of numbers delimited by commas and return a boolean along with a result
            result = 0;

            try
            {
                // convert our comma delimited string to an array of ints
                int[] _nums = Array.ConvertAll(inputString.Split(','), int.Parse);
                long _result = 0;

                if (_nums.Length > 0)
                {
                    for (int i = 0; i <= _nums.Length-1; i++)
                    {
                        /* 
                         * convert each number item in the array to a bitmask...
                         * ... then add the previous bitmask together with the current item
                         */
                        _result = i == 0 ? (1 << _nums[i]) : (1 << _nums[i]) + _result;
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
                Program.Logger("EXCEPTION", ex.Message);
#if DEBUG
                throw new Exception(ex.Message);
#endif
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
            if (Program.StringEquals(bitmask, "DualCore"))
            {// avoid CPU0 if threaded
                result = _isHT ? (ulong)0xA : (ulong)0x5;
                Program.Logger("OSOL", String.Format("Parsed core mask to: {0}", AffinityToCoreString(result)));
                return true;
            }
            else if (Program.StringEquals(bitmask, "QuadCore"))
            {
                result = _isHT ? (ulong)0xAA : (ulong)0xF;
                Program.Logger("OSOL", String.Format("Parsed core mask to: {0}", AffinityToCoreString(result)));
                return true;
            }
            else if (_isHT && Program.StringEquals(bitmask, "DisableHT"))
            {
                long _aCores = 0;

                for (int i = 0; i <= _physCores * 2; i++)
                {// loop through and accumulate bits
                    _aCores += (1 << i);
                    i++; // .. for every other logical core
                }

                result = (UInt64)_aCores;
                Program.Logger("OSOL", String.Format("Setting core mask to: {0}", AffinityToCoreString(result)));
                return true;
            }
            else if (!String.IsNullOrEmpty(bitmask) && !Program.StringEquals(bitmask, "DisableHT"))
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
                    Program.Logger("OSOL", String.Format("Parsed number mask to cores: {0}", AffinityToCoreString(result)));
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
