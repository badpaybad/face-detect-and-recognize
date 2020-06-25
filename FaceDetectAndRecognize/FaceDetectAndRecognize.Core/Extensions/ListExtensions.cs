using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FaceDetectAndRecognize.Core.Extensions
{
    public static class ListExtensions
    {
        public static void SplitToRun<T>(this List<T> allItems, int batchSize, Action<List<T>, int> action)
        {
            if (allItems == null || allItems.Count == 0) return;
            var total = allItems.Count;
            var skip = 0;
            int batchIndex = 0;
            while (true)
            {
                var batch = allItems.Skip(skip).Take(batchSize).Distinct().ToList();

                if (batch == null || batch.Count == 0) { break; }

                action(batch, batchIndex);

                batchIndex++;

                skip = skip + batchSize;

                total = total - batchSize;
            }
        }

    }
}
