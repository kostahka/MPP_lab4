using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Autogenerated;
using TestClasses;

namespace Autogenerated.Tests
{
    [TestFixture]
    public class Class4Test
    {
        private Class4 Class4TestObject;
        [SetUp]
        public void SetUp()
        {
            Class4TestObject = new Class4();
        }

        [Test]
        public void Method1Test()
        {
            //Act
            Class4TestObject.Method1();
            //Assert
            Assert.Fail("autogenerated");
        }

        [Test]
        public void Method2Test()
        {
            //Act
            Class4TestObject.Method2();
            //Assert
            Assert.Fail("autogenerated");
        }
    }
}