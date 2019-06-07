using System;
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

                foreach (var item in new ManagementObjectSearcher("SELECT * from Win32_Processor").Get())
                {// accumulate from Win32_Processor count to get number of physical cores
                    _pCores += Int32.Parse(item["NumberOfCores"].ToString());
                }

                ProcessUtils.Logger("OSOL", $"Physical CPUs: {_pCores} / Logical CPUs: {_lCores}");
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

        public static string AffinityToCoreString(long inputArray)
        {// return a string of numbered cores from a ulong affinity mask
            var _saneInput = inputArray;
            var maxResult = 0xFFFFFFFF;
            // apply sanity to input (truncate anything beyond 32 bits)
            if (_saneInput > maxResult)
                _saneInput = maxResult;
            else if (_saneInput <= 0)
                _saneInput = 0;

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

            return "";
        }

        public static bool TryParseCoreString(string inputString, out long result)
        {// take a string of numbers delimited by commas and return a boolean along with a result
            result = 0;

            try
            {
                // convert our comma delimited string to an array of ints
                int[] _nums = !string.IsNullOrWhiteSpace(inputString) ?
                    Array.ConvertAll(inputString.Split(','), int.Parse) : new int[] { };
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

                        Debug.WriteLine(
                            $"Iterating {_it} -> 0x{_op:X} + 0x{_before:X} = 0x{_result:X} [{Convert.ToString(_result, 2)}]"
                        );
                    }

                    Debug.WriteLine($"Result: {_result} -> 0x{_result:X} [{Convert.ToString(_result, 2)}]");

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
            if (ProcessUtils.OrdinalEquals(bitmask, "DualCore"))
            {// avoid CPU0 if threaded
                result = _isHT ? 0xA : 0x5;
                ProcessUtils.Logger("OSOL", $"Parsed core mask to: {AffinityToCoreString(result)}");
                return true;
            }
            else if (ProcessUtils.OrdinalEquals(bitmask, "QuadCore"))
            {
                result = _isHT ? 0xAA : 0xF;
                ProcessUtils.Logger("OSOL", $"Parsed core mask to: {AffinityToCoreString(result)}");
                return true;
            }
            else if (_isHT && ProcessUtils.OrdinalEquals(bitmask, "DisableHT"))
            {
                long? _aCores = 0;

                for (int i = 0; i <= _physCores * 2; i++)
                {// loop through and accumulate bits
                    _aCores += (1 << i);
                    i++; // .. for every other logical core
                }

                result = (long)_aCores;
                ProcessUtils.Logger("OSOL", $"Setting core mask to: {AffinityToCoreString(result)}");
                return true;
            }
            else if (!string.IsNullOrEmpty(bitmask) && !ProcessUtils.OrdinalEquals(bitmask, "DisableHT"))
            {// just convert what's there if possible
                // pass string along to TryParseCorestring first
                if (bitmask.Length > 1
                    && ProcessUtils.OrdinalContains(",", bitmask) && TryParseCoreString(bitmask, out long _stringResult))
                {// try parsing as a core string - ints delimited by commas
                    ProcessUtils.Logger("OSOL", $"Parsed core mask: {AffinityToCoreString(_stringResult)}");
                    result = _stringResult; // copy our output externally
                }
                else if (bitmask.Length > 0
                    && !ProcessUtils.OrdinalContains(",", bitmask) && long.TryParse(bitmask, out result))
                {// attempt to parse using ulong conversion
                    ProcessUtils.Logger("OSOL", $"Parsed number mask to cores: {AffinityToCoreString(result)}");
                }
                else if (bitmask.Length > 1
                    && ProcessUtils.OrdinalContains("0x", bitmask)
                    && Int64.TryParse(bitmask.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result))
                {// last ditch attempt to parse using hex conversion (in ordinal/invariant mode)
                    ProcessUtils.Logger("OSOL", $"Parsed hex mask to cores: {AffinityToCoreString(result)}");
                }

                // sanity check our max return
                if (result > maxResult)
                    result = maxResult;

                return result >= 0 ? true : false;
            }
            else if (!string.IsNullOrEmpty(bitmask))
            {// default to single core if nothing else is a valid parse
                result = 1;
                return true;
            }

            return false;
        }
    }
}
