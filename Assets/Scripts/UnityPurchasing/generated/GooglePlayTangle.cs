// WARNING: Do not modify! Generated file.

namespace UnityEngine.Purchasing.Security {
    public class GooglePlayTangle
    {
        private static byte[] data = System.Convert.FromBase64String("IXvxqjJrNQAheKeFjeAl2UThfkijdlEjLSZcwN4ZvERb4YUzQN1qah6bbZCV1pPmEvtF6H+fa6WAEgOBabUc3MEL5c5VThOb2clYNRU4YxAQk52SohCTmJAQk5OSYsoZ9KD26og6KpvvHp9AHQwCCK2sJE3hHojQAi5n6AS1bTNCkM1GtLj0MQRzGw7I9Nsl0YIrBR/dGqIHyQIWiorfVelX4ijTgqUmiChKgZVFB3gnOV6johCTsKKflJu4FNoUZZ+Tk5OXkpFep++TB5st/ALbnBUt090Pec5Jsp8ydrIyZUo7nLEfRHKzgf0vgDKl5RD31wnhxkC4M5loTjhkSVApTChum71w+ZfDL7bzeZLxTl2FcTP3eYm+00mOfl2CZZCRk5KT");
        private static int[] order = new int[] { 4,5,7,6,5,10,13,9,9,10,11,11,12,13,14 };
        private static int key = 146;

        public static readonly bool IsPopulated = true;

        public static byte[] Data() {
        	if (IsPopulated == false)
        		return null;
            return Obfuscator.DeObfuscate(data, order, key);
        }
    }
}
