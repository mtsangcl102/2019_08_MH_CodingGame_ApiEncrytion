using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace Tests
{
    public class TestHelper
    {
        public static IEnumerator Timeout(double seconds)
        {
            yield return Timeout(null, seconds);
        }
        
        public static IEnumerator Timeout(Func<bool> endCondition = null, double seconds = 2.0)
        {
            var s = DateTime.UtcNow;
            bool isTimeout = endCondition != null;
            while ((DateTime.UtcNow - s).TotalSeconds < seconds)
            {
                var isEnded = endCondition != null && endCondition();
                if (isEnded)
                {
                    isTimeout = false;
                    break;
                }

                yield return null;
            }

            if (isTimeout)
            {
                Debug.Log($"Timeout");
                Assert.IsTrue(false);
            }
        }
    }
}