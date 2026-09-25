using NUnit.Framework;

namespace TrackpadNavigation.Tests
{
    public sealed class EditorMemberTests
    {
        class BaseState
        {
            int scale = 7;
            int Offset
            {
                get; set;
            } = 12;
            object Value = new object();
            int ReadScale() => scale;
        }

        sealed class DerivedState : BaseState
        {
            public object Value => null;
        }

        [Test]
        public void InheritedPrivateStateAndNullPropertiesKeepTheirMeaning()
        {
            var state = new DerivedState();
            Assert.That(EditorMember.Get(state, "scale"), Is.EqualTo(7));
            Assert.That(EditorMember.Method(state, "ReadScale").Invoke(state, null), Is.EqualTo(7));
            Assert.That(EditorMember.Writable(state, "Offset", typeof(int)), Is.True);
            EditorMember.Set(state, "Offset", 20);
            Assert.That(EditorMember.Get(state, "Offset"), Is.EqualTo(20));
            Assert.That(EditorMember.Get(state, "Value"), Is.Null, "A null property must not fall back to a same-named base field");
            Assert.That(EditorMember.Writable(state, "Value", typeof(object)), Is.False);
            Assert.That(EditorMember.Writable(state, "Offset", typeof(float)), Is.False);
            Assert.That(EditorMember.Get(state, "Missing"), Is.Null);
            Assert.That(EditorMember.Get(state, "Missing"), Is.Null);
        }
    }
}
