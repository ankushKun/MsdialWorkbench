﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace CompMs.Common.DataStructure.Tests
{
    [TestClass()]
    public class VisitorTests
    {
        [TestMethod()]
        public void VisitorTest() {
            var visitor = new IntToStringVisitor();
            var intDecomposer = new IntDecomposer();
            var intListDecomposer = new IntListDecomposer();

            var item1 = new Visitable<int>(100);
            Assert.AreEqual("100", item1.Accept(visitor, intDecomposer));

            var item2 = new Visitable<List<int>>(new List<int> { 1, 2, 3, });
            Assert.AreEqual("1,2,3", item2.Accept(visitor, intListDecomposer));
        }
    }

    internal class IntDecomposer : IDecomposer<string, int, int>
    {
        public string Decompose(IAcyclicVisitor visitor, int element) {
            if (visitor is IVisitor<string, int> vis) {
                return vis.Visit(element);
            }
            return string.Empty;
        }
    }

    internal class IntListDecomposer : IDecomposer<string, List<int>, int>
    {
        public string Decompose(IAcyclicVisitor visitor, List<int> element) {
            if (visitor is IVisitor<string, int> vis) {
                return string.Join(",", element.Select(vis.Visit));
            }
            return string.Empty;
        }
    }

    internal class IntToStringVisitor : IVisitor<string, int>
    {
        public string Visit(int item) {
            return item.ToString();
        }
    }

    internal class Visitable<TElement> : IVisitableElement<TElement>
    {
        private readonly TElement _element;

        public Visitable(TElement element) {
            _element = element;
        }

        public TResult Accept<TResult, TDecomposed>(IAcyclicVisitor visitor, IDecomposer<TResult, TElement, TDecomposed> decomposer) {
            return decomposer.Decompose(visitor, _element);
        }

        public TResult Accept<TResult, TDecomposed>(IVisitor<TResult, TDecomposed> visitor, IDecomposer<TResult, TElement, TDecomposed> decomposer) {
            return decomposer.Decompose(visitor, _element);
        }
    }
}
