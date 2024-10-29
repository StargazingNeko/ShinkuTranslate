using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ShinkuTranslate.misc {
    class MyException : Exception {
        public MyException(string message) : base(message) { }
    }
}
