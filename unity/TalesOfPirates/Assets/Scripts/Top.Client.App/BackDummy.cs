using UnityEngine;

namespace Top.Client.App
{
    /// <summary>
    /// Which dummy of the rig a held thing hangs on when it is not in a hand: the client's own table,
    /// turned into code. <br/>
    /// A hero in a safe zone puts what he is holding on his back, and where on his back is the client's
    /// business and nobody else's: its characters have three points for it - 21 across the back, 22 for
    /// a shield or a bow, 23 for something small - and which one a thing takes is decided by the kind of
    /// weapon it is and which hand it was in.
    /// <br/>
    /// And a thing the table has no place for does not go on the back at all: it stays in the hand. That
    /// is not a special case for anything - a coral is simply a kind of thing the table never mentions,
    /// so a caster keeps it in his hand in a safe zone, which is how the client behaved. The rule is one
    /// rule: ask the table, and a thing with no answer has no back.
    /// </summary>
    public static class BackDummy
    {
        /// <summary>
        /// Where a thing of this kind hangs when it is on the back, or nothing when it does not. <br/>
        /// The numbers are the client's own, written out of its CCharacter::GetWeaponBackDummy exactly as
        /// it stands there - including the kinds it leaves out, which is what keeps them in the hand.
        /// </summary>
        public static string For(int weaponType, bool left)
        {
            if (left)
            {
                switch (weaponType)
                {
                    case 2:
                    case 5:
                    case 9:
                    case 10:
                        return "dummy_21";

                    case 1:
                    case 3:
                    case 11:
                        return "dummy_22";

                    case 4:
                    case 7:
                    case 8:
                        return "dummy_23";

                    default:
                        return null;
                }
            }

            switch (weaponType)
            {
                case 1:
                case 2:
                case 5:
                case 9:
                case 10:
                    return "dummy_21";

                case 3:
                case 11:
                case 18:
                case 19:
                    return "dummy_22";

                case 4:
                case 7:
                case 8:
                    return "dummy_23";

                default:
                    return null;
            }
        }

        /// <summary>
        /// Whether a kind of thing has a place on the back at all, either hand. Asked by anything that
        /// wants to know before a hero is in hand - a thing with none never leaves the hand.
        /// </summary>
        public static bool Any(int weaponType)
        {
            return For(weaponType, false) != null || For(weaponType, true) != null;
        }

        /// <summary>The dummies this knows of, for a scene that has to have them to hang anything.</summary>
        public static readonly string[] All = { "dummy_21", "dummy_22", "dummy_23" };
    }
}
