using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestClasses
{
    public interface IInterface1
    {
        int GetNum();
    }
    public class Class1
    {
        private IInterface1 interface1;
        public Class1(IInterface1 interface1)
        {
            this.interface1 = interface1;
        }
        public string Method1(string s)
        {
            return s + interface1.GetNum();
        }

        public void Method2()
        {

        }
    }
    public class Class2
    {
        public int Method1(int a, int b)
        {
            return a + b;
        }

        public void Method2()
        {

        }
    }

    public class Class3
    {
        public void Method1()
        {

        }

        public void Method2()
        {

        }
    }
}
