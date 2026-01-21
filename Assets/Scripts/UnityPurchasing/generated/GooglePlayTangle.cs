// WARNING: Do not modify! Generated file.

namespace UnityEngine.Purchasing.Security {
    public class GooglePlayTangle
    {
        private static byte[] data = System.Convert.FromBase64String("uqeLF5/qDEgYhgHNUPagLdqYwPzrN+mOH+f7qoRQYOZ0zd1ojEhYtf9JhkvxS0Jra3mrMNyYocdPk4114zbQOy+SVVZlVtXJ29eJJvsTIsajsib+ViWHgeniFqhFJpm6tXXMP4NvMSmfue6yIxrEtgXTB0Axt7IV4ZLtRz/iVJe8q+BqnxBGXu2r2wtT4WJBU25laknlK+WUbmJiYmZjYPDEW7g/DDc41R7ayduoy1AbkeGo6OtU/wygNNP5qpXnTwhfmSmfc4rhYmxjU+FiaWHhYmJjtBZjuMD12nJ32R7OtnOry2KPxUSuBw/fIByL1FoDs7DqYay6VJ5RBK5lL7Z2GCxG6+IRX7cPeBYB3tI0CBDLyhDFDk6tk5RUaxqyZmFgYmNi");
        private static int[] order = new int[] { 7,12,4,9,8,9,13,7,9,11,12,13,12,13,14 };
        private static int key = 99;

        public static readonly bool IsPopulated = true;

        public static byte[] Data() {
        	if (IsPopulated == false)
        		return null;
            return Obfuscator.DeObfuscate(data, order, key);
        }
    }
}
