using System;
using OriginSteamOverlayLauncher;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OSOL.UnitTests
{
    [TestClass]
    public class ProgramTests
    {
        [TestMethod]
        public void StringEquals_Equals_ReturnTrue()
        {
            var _string1 = "\\\"C:\\Sample\\Path\\To\\Game\\Game.exe\" ";
            var _string2 = "\\\"C:\\Sample\\Path\\To\\Game\\Game.exe\" ";

            var result = ProcessUtils.OrdinalEquals(_string1, _string2);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void StringEquals_Equals_ReturnFalse()
        {
            var _string1 = "\\\"C:\\Sample\\Path\\To\\Game\\Game.exe\" ";
            var _string2 = "\\\"D:\\Another\\Sample\\Path\\To\\Another\\Game\\Game.exe\" ";

            var result = ProcessUtils.OrdinalEquals(_string1, _string2);

            Assert.IsFalse(result);
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

            var result1 = SettingsData.ValidateURI(_input1);
            var result2 = SettingsData.ValidateURI(_input2);
            var result3 = SettingsData.ValidateURI(_input3);
            var result = result1 && result2 && result3;

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void PathIsURI_IsNotValid_ReturnsFalse()
        {
            var _input1 = "a:/unixstyle/path";
            var _input2 = "/b/another/unixstyle/path";
            var _input3 = "proto;//stuff/";

            var result1 = SettingsData.ValidateURI(_input1);
            var result2 = SettingsData.ValidateURI(_input2);
            var result3 = SettingsData.ValidateURI(_input3);
            var result = result1 || result2 || result3;

            Assert.IsFalse(result);
        }
    }
}
