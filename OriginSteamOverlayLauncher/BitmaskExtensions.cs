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

                ProcessUtils.Logger("OSOL", String.Format("Physical CPUs: {0} / Logical CPUs: {1}", _pCores, _lCores));
                NumberOfPCores = _pCores > 0 ? _pCores : 1; // share our physical core count externally

                if (_pCores != _lCores)
                {// if physical cores and logical cores differ we're hyperthreaded
                    return true;
                }
            }
            catch (ManagementException ex)
            {
                ProcessUtils.Logger("EXCEPTION", ex.Message);
#if DEBUG
                //throw new Exception(ex.Message);
#endif
            }

            NumberOfPCores = 1;
            return false;
        }

        public static String AffinityToCoreString(long inputArray)
        {// return a string of numbered cores from a ulong affinity mask
            var _saneInput = inputArray;
            var maxResult = 0xFFFFFFFF;
            // apply sanity to input (truncate anything beyond 32 bits)
            if ((long)_saneInput > (long)maxResult)
                _saneInput = (long)maxResult;
            else if (_saneInput < 0)
                _saneInput = (long)0;

            char[] _chr = Convert.ToString(_saneInput, 2).ToCharArray();
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

        public static bool TryParseCoreString(String inputString, out long result)
        {// take a string of numbers delimited by commas and return a boolean along with a result
            result = 0;

            try
            {
                // convert our comma delimited string to an array of ints
                int[] _nums = Array.ConvertAll(inputString.Split(','), int.Parse);
                long _result = 0;

                if (_nums.Length > 0)
                {
                    for (int i = 0; i < _nums.Length; i++)
                    {// append bitshifted int to existing bitmask
                        int _it = _nums[i];
                        if (_it > 31)
                            _it = -1; // truncate silly inputs
                        
                        var _op = _it >= 0 ? (1ul << _it) : 0; // prevent mangling
                        long _before = _result;
                        _result += (long)_op;

                        Debug.WriteLine(String.Format(
                            "Iterating {0} -> 0x{1:X} + 0x{2:X} = 0x{3:X} [{4}]", _it, _op, _before, _result, Convert.ToString(_result, 2)
                            ));
                    }

                    Debug.WriteLine(String.Format("Result: {0} -> 0x{0:X} [{1}]", _result, Convert.ToString(_result, 2)));

                    if (_result > 0)
                    {
                        result = _result;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                ProcessUtils.Logger("EXCEPTION", ex.Message);
#if DEBUG
                if (ex is FormatException)
                {// do not throw if input is invalid - default to 0
                    return false;
                }
                else
                {
                    throw new Exception(ex.Message);
                }
#endif
            }

            return false;
        }

        public static bool TryParseAffinity(string bitmask, out long result)
        {// convert bitmask string to ulong internally
            result = 0;
            bool _isHT = IsCPUHyperthreaded(out int _physCores);
            // for sanity cap our max input to 32 cores (core0 - core31)
            var maxResult = 0xFFFFFFFF;

            // provide shortcuts for common affinity masks (be smart about HT)
            if (ProcessUtils.StringEquals(bitmask, "DualCore"))
            {// avoid CPU0 if threaded
                result = _isHT ? (long)0xA : (long)0x5;
                ProcessUtils.Logger("OSOL", String.Format("Parsed core mask to: {0}", AffinityToCoreString(result)));
                return true;
            }
            else if (ProcessUtils.StringEquals(bitmask, "QuadCore"))
            {
                result = _isHT ? (long)0xAA : (long)0xF;
                ProcessUtils.Logger("OSOL", String.Format("Parsed core mask to: {0}", AffinityToCoreString(result)));
                return true;
            }
            else if (_isHT && ProcessUtils.StringEquals(bitmask, "DisableHT"))
            {
                long _aCores = 0;

                for (int i = 0; i <= _physCores * 2; i++)
                {// loop through and accumulate bits
                    _aCores += (1 << i);
                    i++; // .. for every other logical core
                }

                result = (long)_aCores;
                ProcessUtils.Logger("OSOL", String.Format("Setting core mask to: {0}", AffinityToCoreString(result)));
                return true;
            }
            else if (!String.IsNullOrEmpty(bitmask) && !ProcessUtils.StringEquals(bitmask, "DisableHT"))
            {// just convert what's there if possible
                // pass string along to TryParseCoreString first
                if (bitmask.Length > 1
                    && ProcessUtils.OrdinalContains(",", bitmask) && TryParseCoreString(bitmask, out long _stringResult))
                {// try parsing as a core string - ints delimited by commas
                    ProcessUtils.Logger("OSOL", String.Format("Parsed core mask: {0}", AffinityToCoreString(_stringResult)));
                    result = _stringResult; // copy our output externally
                }
                else if (bitmask.Length > 0
                    && !ProcessUtils.OrdinalContains(",", bitmask) && Int64.TryParse(bitmask, out result))
                {// attempt to parse using ulong conversion
                    ProcessUtils.Logger("OSOL", String.Format("Parsed number mask to cores: {0}", AffinityToCoreString(result)));
                }
                else if (bitmask.Length > 1
                    && ProcessUtils.OrdinalContains("0x", bitmask)
                    && Int64.TryParse(bitmask.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result))
                {// last ditch attempt to parse using hex conversion (in ordinal/invariant mode)
                    ProcessUtils.Logger("OSOL", String.Format("Parsed hex mask to cores: {0}", AffinityToCoreString(result)));
                }

                // sanity check our max return
                if (result > maxResult)
                    result = maxResult;

                return result >= 0 ? true : false;
            }
            else if (!String.IsNullOrEmpty(bitmask))
            {// default to single core if nothing else is a valid parse
                result = (long)1;
                return true;
            }

            return false;
        }
    }
}
