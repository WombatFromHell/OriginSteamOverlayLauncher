using System;
using OriginSteamOverlayLauncher;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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

            var result = ProcessUtils.CompareCommandlines(_cmdline1, _cmdline2);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void CompareCommandlines_Equals_ReturnsFalse()
        {
            var _cmdline1 = " -arg0 -arg1 -arg2 -TEST_AUTH_STRING=9876543210";
            var _cmdline2 = " -arg0 -arg1 -arg2 -TEST_AUTH_STRING=0123456789";

            var result = ProcessUtils.CompareCommandlines(_cmdline1, _cmdline2);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void RemoveInPlace_ValidMatch_ReturnsNotEmpty()
        {
            var _match = "\\\"C:\\Sample\\Path\\To\\Game\\Game.exe\" ";
            var _cmdline = "\\\"C:\\Sample\\Path\\To\\Game\\Game.exe\"  -arg0 -arg1 -arg2 -TEST_AUTH_STRING=0123456789";

            var result = String.IsNullOrEmpty(ProcessUtils.RemoveInPlace(_cmdline, _match));

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void RemoveInPlace_ValidMatch_ReturnsEmpty()
        {
            var _match = "\\\"C:\\Sample\\Path\\To\\Game\\Game.exe\" ";
            var _cmdline = "\\\"C:\\Sample\\Path\\To\\Game\\Game.exe\" ";

            var result = String.IsNullOrEmpty(ProcessUtils.RemoveInPlace(_cmdline, _match));

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void RemoveInPlace_InvalidMatch_ReturnsEmpty()
        {
            var _match = "\\\"C:\\Sample\\Path\\To\\Game\\Game.exe\" ";
            var _cmdline = "\\\"C:/Sample/Path/To/Game/Game.exe\"  -arg0 -arg1 -arg2 -TEST_AUTH_STRING=0123456789";

            var result = String.IsNullOrEmpty(ProcessUtils.RemoveInPlace(_cmdline, _match));

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void StringEquals_Equals_ReturnTrue()
        {
            var _string1 = "\\\"C:\\Sample\\Path\\To\\Game\\Game.exe\" ";
            var _string2 = "\\\"C:\\Sample\\Path\\To\\Game\\Game.exe\" ";

            var result = ProcessUtils.StringEquals(_string1, _string2);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void StringEquals_Equals_ReturnFalse()
        {
            var _string1 = "\\\"C:\\Sample\\Path\\To\\Game\\Game.exe\" ";
            var _string2 = "\\\"D:\\Another\\Sample\\Path\\To\\Another\\Game\\Game.exe\" ";

            var result = ProcessUtils.StringEquals(_string1, _string2);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ConvertUnixToDosPath_ValidInput_ReturnsUnique()
        {
            var _cmdline = "\\\"C:/Sample/Path/To/Game/Game.exe\"";

            var result = String.Equals(ProcessUtils.ConvertUnixToDosPath(_cmdline), _cmdline);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ConvertUnixToDosPath_InvalidInput_ReturnsOriginalPath()
        {
            var _cmdline = "\\\"C:\\Sample\\Path\\To\\Game\\Game.exe\"";

            var result = String.Equals(ProcessUtils.ConvertUnixToDosPath(_cmdline), _cmdline);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void CliArgExists_HasCapitalArg1_ReturnsTrue()
        {
            var _args = new string[] { "/arg0", "-ARG1", "/Arg2" }; // try typical permutations

            var result = ProcessUtils.CliArgExists(_args, "arg1");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void CliArgExists_HasMixedArg2_ReturnsTrue()
        {
            var _args = new string[] { "/arg0", "-ARG1", "/Arg2" };

            var result = ProcessUtils.CliArgExists(_args, "arg2");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void CliArgExists_HasNoArg6_ReturnsFalse()
        {
            var _args = new string[] { "/arg0", "-ARG1", "/Arg2" };

            var result = ProcessUtils.CliArgExists(_args, "arg6");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void CliArgExists_HasInvalidArg0_ReturnsFalse()
        {
            var _args = new string[] { "/arg0", "-ARG1", "/Arg2" };

            var result = ProcessUtils.CliArgExists(_args, "/Arg0");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void PathIsURI_IsValid_ReturnsTrue()
        {
            var _input1 = "battlenet://SC2/";
            var _input2 = "battlenet://Pro/";
            var _input3 = "battlenet://WoW/";

            var result1 = ProcessUtils.PathIsURI(_input1);
            var result2 = ProcessUtils.PathIsURI(_input2);
            var result3 = ProcessUtils.PathIsURI(_input3);
            var result = result1 && result2 && result3;

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void PathIsURI_IsNotValid_ReturnsFalse()
        {
            var _input1 = "a:/unixstyle/path";
            var _input2 = "/b/another/unixstyle/path";
            var _input3 = "proto;//stuff/";

            var result1 = ProcessUtils.PathIsURI(_input1);
            var result2 = ProcessUtils.PathIsURI(_input2);
            var result3 = ProcessUtils.PathIsURI(_input3);
            var result = result1 || result2 || result3;

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void RemoveInPlace_ValidMatch_ReturnsTrue()
        {
            // typical use-case where we're grabbing only the cmdline args
            var _container = "\\\"C:\\Program Files (x86)\\Steam\\steamapps\\common\\TestPath\\TestGame.exe\\\" -arg0 /Arg1 -ARG2";
            var _match = "\\\"C:\\Program Files (x86)\\Steam\\steamapps\\common\\TestPath\\TestGame.exe\\\" ";

            var _output = ProcessUtils.RemoveInPlace(_container, _match);
            var result = !String.IsNullOrEmpty(_output);

            Assert.IsTrue(result);
        }
    }
}
