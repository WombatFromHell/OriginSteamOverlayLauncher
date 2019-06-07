using System;
using OriginSteamOverlayLauncher;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OSOL_UnitTests
{
    [TestClass]
    public class BitmaskExtensionsUnitTests
    {
        [TestMethod]
        public void AffinityToCoreString_QuadCoreBits_ReturnsTrue()
        {
            // use first four bits (4 cores)
            var _inputArr = 0xF;
            var match = "0,1,2,3";

            var result = BitmaskExtensions.AffinityToCoreString((long)_inputArr);

            // use an exact string equality comparison
            Assert.IsTrue(string.Equals(result, match));
        }

        [TestMethod]
        public void AffinityToCoreString_16CoreBitsAlt_ReturnsTrue()
        {
            var _inputArr = 0xAAAA; // core1-core15 alternating
            var match = "1,3,5,7,9,11,13,15";

            var result = BitmaskExtensions.AffinityToCoreString((long)_inputArr);

            Assert.IsTrue(string.Equals(result, match));
        }

        [TestMethod]
        public void AffinityToCoreString_SanityCheck_ReturnsTrue()
        {
            // use an insane input to check for sane output
            long inputVal = 0x1FFFFFFFFFFFF; // core0-core47
            var match = "0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31";

            var result = BitmaskExtensions.AffinityToCoreString((long)inputVal);

            // test for sane output (should truncate to first 32 cores [0-31])
            Assert.IsTrue(string.Equals(result, match));
        }

        [TestMethod]
        public void TryParseCoreString_QuadCoreString_ReturnsTrue()
        {
            var _inputString = "0,1,2,3";
            var _match = 0xF; // core0-core3

            var _isCoreString = BitmaskExtensions.TryParseCoreString(_inputString, out long _result);
            var result = Int64.Equals((long)_match, (long)_result);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void TryParseCoreString_InvalidInput_ReturnsFalse()
        {
            var _inputString = "adsfkjgasdfkgdfkasgj";
            var _match = 0xF;

            var _isCoreString = BitmaskExtensions.TryParseCoreString(_inputString, out long _result);
            var result = _isCoreString && Int64.Equals((long)_match, (long)_result);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void TryParseAffinity_16CoreBitmask_ReturnsTrue()
        {
            var _inputString = "1,3,5,7,9,11,13,15,17,19,21,23,25,27,29,31";
            long _match = 0xAAAAAAAA; // core1-core31 alternating

            var _isCoreString = BitmaskExtensions.TryParseAffinity(_inputString, out long _result);
            var result = _isCoreString && Int64.Equals(_match, _result);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void TryParseAffinity_OverflowSanity_ReturnsTrue()
        {
            // overflow test
            var _inputString = "1,3,5,7,9,11,13,15,17,19,21,23,25,27,29,31,33,35,37,39,41,43,45,47";
            long _match = 0xAAAAAAAA; // core0-core31 alternating

            var _isCoreString = BitmaskExtensions.TryParseAffinity(_inputString, out long _result);
            // should truncate to 32 bits [0-31]
            var result = _isCoreString && Int64.Equals(_match, _result);

            Assert.IsTrue(result);
        }
    }
}
