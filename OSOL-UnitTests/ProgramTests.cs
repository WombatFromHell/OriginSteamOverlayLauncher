using System;
using OriginSteamOverlayLauncher;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace OSOL.UnitTests
{
    [TestClass]
    public class ProgramTests
    {
        [TestMethod]
        public void CompareCommandlines_Equals_ReturnsTrue()
        {
            var _cmdline1 = " -arg0 -arg1 -arg2 -TEST_AUTH_STRING=0123456789";
            var _cmdline2 = " -arg0 -arg1 -arg2 -TEST_AUTH_STRING=0123456789";

            var result = Program.CompareCommandlines(_cmdline1, _cmdline2);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void CompareCommandlines_Equals_ReturnsFalse()
        {
            var _cmdline1 = " -arg0 -arg1 -arg2 -TEST_AUTH_STRING=9876543210";
            var _cmdline2 = " -arg0 -arg1 -arg2 -TEST_AUTH_STRING=0123456789";

            var result = Program.CompareCommandlines(_cmdline1, _cmdline2);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void RemoveInPlace_ValidMatch_ReturnsNotEmpty()
        {
            var _match = "\\\"C:\\Sample\\Path\\To\\Game\\Game.exe\" ";
            var _cmdline = "\\\"C:\\Sample\\Path\\To\\Game\\Game.exe\"  -arg0 -arg1 -arg2 -TEST_AUTH_STRING=0123456789";

            var result = String.IsNullOrEmpty(Program.RemoveInPlace(_cmdline, _match));

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void RemoveInPlace_ValidMatch_ReturnsEmpty()
        {
            var _match = "\\\"C:\\Sample\\Path\\To\\Game\\Game.exe\" ";
            var _cmdline = "\\\"C:\\Sample\\Path\\To\\Game\\Game.exe\" ";

            var result = String.IsNullOrEmpty(Program.RemoveInPlace(_cmdline, _match));

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void RemoveInPlace_InvalidMatch_ReturnsEmpty()
        {
            var _match = "\\\"C:\\Sample\\Path\\To\\Game\\Game.exe\" ";
            var _cmdline = "\\\"C:/Sample/Path/To/Game/Game.exe\"  -arg0 -arg1 -arg2 -TEST_AUTH_STRING=0123456789";

            var result = String.IsNullOrEmpty(Program.RemoveInPlace(_cmdline, _match));

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void StringEquals_Equals_ReturnTrue()
        {
            var _string1 = "\\\"C:\\Sample\\Path\\To\\Game\\Game.exe\" ";
            var _string2 = "\\\"C:\\Sample\\Path\\To\\Game\\Game.exe\" ";

            var result = Program.StringEquals(_string1, _string2);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void StringEquals_Equals_ReturnFalse()
        {
            var _string1 = "\\\"C:\\Sample\\Path\\To\\Game\\Game.exe\" ";
            var _string2 = "\\\"D:\\Another\\Sample\\Path\\To\\Another\\Game\\Game.exe\" ";

            var result = Program.StringEquals(_string1, _string2);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ConvertUnixToDosPath_ValidInput_ReturnsUnique()
        {
            var _cmdline = "\\\"C:/Sample/Path/To/Game/Game.exe\"";

            var result = String.Equals(Program.ConvertUnixToDosPath(_cmdline), _cmdline);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ConvertUnixToDosPath_InvalidInput_ReturnsOriginalPath()
        {
            var _cmdline = "\\\"C:\\Sample\\Path\\To\\Game\\Game.exe\"";

            var result = String.Equals(Program.ConvertUnixToDosPath(_cmdline), _cmdline);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void CliArgExists_HasCapitalArg1_ReturnsTrue()
        {
            var _args = new string[] { "/arg0", "-ARG1", "/Arg2" }; // try typical permutations

            var result = Program.CliArgExists(_args, "arg1");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void CliArgExists_HasMixedArg2_ReturnsTrue()
        {
            var _args = new string[] { "/arg0", "-ARG1", "/Arg2" };

            var result = Program.CliArgExists(_args, "arg2");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void CliArgExists_HasNoArg6_ReturnsFalse()
        {
            var _args = new string[] { "/arg0", "-ARG1", "/Arg2" };

            var result = Program.CliArgExists(_args, "arg6");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void CliArgExists_HasInvalidArg0_ReturnsFalse()
        {
            var _args = new string[] { "/arg0", "-ARG1", "/Arg2" };

            var result = Program.CliArgExists(_args, "/Arg0");

            Assert.IsFalse(result);
        }
    }
}
