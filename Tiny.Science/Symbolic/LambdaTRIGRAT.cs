﻿using System;
using System.Collections;

using Math = Tiny.Science.Numeric.Math;

namespace Tiny.Science.Symbolic
{
    internal class LambdaTRIGRAT : Lambda
    {
        public override int Eval( Stack st )
        {
            int narg = GetNarg( st );

            var f = GetAlgebraic( st );

            f = f.Rat();

            Debug( "Rational: " + f );

            f = ( new ExpandUser() ).SymEval( f );

            Debug( "User Function expand: " + f );

            f = ( new TrigExpand() ).SymEval( f );

            Debug( "Trigexpand: " + f );

            f = ( new NormExp() ).SymEval( f );

            Debug( "Norm: " + f );

            f = ( new TrigInverseExpand() ).SymEval( f );

            Debug( "Triginverse: " + f );

            f = ( new SqrtExpand() ).SymEval( f );

            Debug( "Sqrtexpand: " + f );

            st.Push( f );

            return 0;
        }
    }

    internal class LambdaTRIGEXP : Lambda
    {
        public override int Eval( Stack st )
        {
            int narg = GetNarg( st );
            var f = GetAlgebraic( st );

            f = f.Rat();

            Debug( "Rational: " + f );

            f = ( new ExpandUser() ).SymEval( f );

            Debug( "User Function expand: " + f );

            f = ( new TrigExpand() ).SymEval( f );

            Debug( "Trigexpand: " + f );

            f = ( new NormExp() ).SymEval( f );

            f = ( new SqrtExpand() ).SymEval( f );

            st.Push( f );

            return 0;
        }
    }

    internal class TrigExpand : LambdaAlgebraic
    {
        internal override Algebraic SymEval( Algebraic x )
        {
            if ( x is Polynomial && ( ( Polynomial ) x ).Var is FunctionVariable )
            {
                var xp = ( Polynomial ) x;

                var f = ( FunctionVariable ) xp.Var;

                var la = Session.Proc.Store.GetValue( f.Name );

                if ( la is LambdaAlgebraic && ( ( LambdaAlgebraic ) la ).trigrule != null )
                {
                    try
                    {
                        var rule = ( ( LambdaAlgebraic ) la ).trigrule;

                        var fexp = evalx( rule, f.Var );

                        Algebraic r = Symbol.ZERO;

                        for ( int i = xp.Coeffs.Length - 1; i > 0; i-- )
                        {
                            r = ( r + SymEval( xp[ i ] ) ) * fexp;
                        }

                        if ( xp.Coeffs.Length > 0 )
                        {
                            r = r + SymEval( xp[ 0 ] );
                        }

                        return r;
                    }
                    catch ( Exception e )
                    {
                        throw new SymbolicException( e.ToString() );
                    }
                }
            }

            return x.Map( this );
        }
    }

    internal class SqrtExpand : LambdaAlgebraic
    {
        internal override Algebraic SymEval( Algebraic x )
        {
            if ( !( x is Polynomial ) )
            {
                return x.Map( this );
            }

            var xp = ( Polynomial ) x;

            var item = xp.Var;

            if ( item is Root )
            {
                var cr = ( ( Root ) item ).poly;

                if ( cr.Length() == xp.Degree() + 1 )
                {
                    var xr = new Algebraic[ xp.Degree() + 1 ];

                    Algebraic ratio = null;

                    for ( int i = xr.Length - 1; i >= 0; i-- )
                    {
                        xr[ i ] = xp[ i ].Map( this );

                        if ( i == xr.Length - 1 )
                        {
                            ratio = xr[ i ];
                        }
                        else if ( i > 0 && ratio != null )
                        {
                            if ( !Equals( cr[ i ] * ratio, xr[ i ] ) )
                            {
                                ratio = null;
                            }
                        }
                    }
                    if ( ratio != null )
                    {
                        return xr[ 0 ] - ratio * cr[ 0 ];
                    }
                    else
                    {
                        return new Polynomial( item, xr );
                    }
                }
            }

            Algebraic xf = null;

            if ( item is FunctionVariable && ( ( FunctionVariable ) item ).Name.Equals( "sqrt" ) && ( ( FunctionVariable ) item ).Var is Polynomial )
            {
                var arg = ( Polynomial ) ( ( FunctionVariable ) item ).Var;

                var sqfr = arg.square_free_dec( arg.Var );

                var issquare = !( sqfr.Length > 0 && !sqfr[ 0 ].Equals( arg[ arg.Coeffs.Length - 1 ] ) );

                for ( int i = 2; i < sqfr.Length && issquare; i++ )
                {
                    if ( ( i + 1 ) % 2 == 1 && !sqfr[ i ].Equals( Symbol.ONE ) )
                    {
                        issquare = false;
                    }
                }

                if ( issquare )
                {
                    xf = Symbol.ONE;

                    for ( int i = 1; i < sqfr.Length; i += 2 )
                    {
                        if ( !sqfr[ i ].Equals( Symbol.ZERO ) )
                        {
                            xf = xf * sqfr[ i ] ^ ( ( i + 1 ) / 2 );
                        }
                    }

                    Algebraic r = Symbol.ZERO;

                    for ( int i = xp.Coeffs.Length - 1; i > 0; i-- )
                    {
                        r = ( r + SymEval( xp[ i ] ) ) * xf;
                    }

                    if ( xp.Coeffs.Length > 0 )
                    {
                        r = r + SymEval( xp[ 0 ] );
                    }

                    return r;
                }
            }

            if ( item is FunctionVariable && ( ( FunctionVariable ) item ).Name.Equals( "sqrt" ) && xp.Degree() > 1 )
            {
                xf = ( ( FunctionVariable ) item ).Var;

                var sq = new Polynomial( item );

                var r = SymEval( xp[ 0 ] );

                Algebraic xv = Symbol.ONE;

                for ( int i = 1; i < xp.Coeffs.Length; i++ )
                {
                    if ( i % 2 == 1 )
                    {
                        r = r + SymEval( xp[ i ] ) * xv * sq;
                    }
                    else
                    {
                        xv = xv * xf;

                        r = r + SymEval( xp[ i ] ) * xv;
                    }
                }

                return r;
            }

            return x.Map( this );
        }
    }

    internal class TrigInverseExpand : LambdaAlgebraic
    {
        public virtual Algebraic divExponential( Algebraic x, FunctionVariable fv, int n )
        {
            var a = new Algebraic[ 2 ];

            a[ 1 ] = x;

            Algebraic xk = Symbol.ZERO;

            for ( int i = n; i >= 0; i-- )
            {
                var kf = FunctionVariable.Create( "exp", fv.Var ).Pow( i );

                a[ 0 ] = a[ 1 ];
                a[ 1 ] = kf;

                Poly.polydiv( a, fv );

                if ( !a[ 0 ].Equals( Symbol.ZERO ) )
                {
                    var kfi = FunctionVariable.Create( "exp", -fv.Var ) ^ ( n - i );

                    xk = xk + a[ 0 ] * kfi;
                }

                if ( Equals( a[ 1 ], Symbol.ZERO ) )
                {
                    break;
                }
            }

            return SymEval( xk );
        }

        internal override Algebraic SymEval( Algebraic x )
        {
            if ( x is Rational )
            {
                var xr = ( Rational ) x;

                if ( xr.den.Var is FunctionVariable
                    && ( ( FunctionVariable ) xr.den.Var ).Name.Equals( "exp" )
                    && ( ( FunctionVariable ) xr.den.Var ).Var.IsComplex() )
                {
                    var fv = ( FunctionVariable ) xr.den.Var;

                    int maxdeg = Math.max( Poly.Degree( xr.nom, fv ), Poly.Degree( xr.den, fv ) );

                    if ( maxdeg % 2 == 0 )
                    {
                        return divExponential( xr.nom, fv, maxdeg / 2 ) / divExponential( xr.den, fv, maxdeg / 2 );
                    }
                    else
                    {
                        var fv2 = new FunctionVariable( "exp", ( ( FunctionVariable ) xr.den.Var ).Var / Symbol.TWO, ( ( FunctionVariable ) xr.den.Var ).Body );

                        Algebraic ex = new Polynomial( fv2, new Algebraic[] { Symbol.ZERO, Symbol.ZERO, Symbol.ONE } );

                        var xr1 = xr.nom.Value( xr.den.Var, ex ) / xr.den.Value( xr.den.Var, ex );

                        return SymEval( xr1 );
                    }
                }
            }

            if ( x is Polynomial && ( ( Polynomial ) x ).Var is FunctionVariable )
            {
                var xp = ( Polynomial ) x;

                Algebraic xf = null;

                var fvar = ( FunctionVariable ) xp.Var;

                if ( fvar.Name.Equals( "exp" ) )
                {
                    var re = fvar.Var.RealPart();
                    var im = fvar.Var.ImagPart();

                    if ( im != Symbol.ZERO )

                    {
                        bool _minus = minus( im );

                        if ( _minus )
                        {
                            im = -im;
                        }

                        var a = FunctionVariable.Create( "exp", re );
                        var b = FunctionVariable.Create( "cos", im );
                        var c = FunctionVariable.Create( "sin", im ) * Symbol.IONE;

                        xf = a * ( _minus ? b - c : b + c );
                    }
                }

                if ( fvar.Name.Equals( "log" ) )
                {
                    var arg = fvar.Var;

                    Algebraic factor = Symbol.ONE, sum = Symbol.ZERO;

                    if ( arg is Polynomial
                        && ( ( Polynomial ) arg ).Degree() == 1
                        && ( ( Polynomial ) arg ).Var is FunctionVariable
                        && ( ( Polynomial ) arg )[ 0 ].Equals( Symbol.ZERO )
                        && ( ( FunctionVariable ) ( ( Polynomial ) arg ).Var ).Name.Equals( "sqrt" ) )
                    {
                        sum = FunctionVariable.Create( "log", ( ( Polynomial ) arg )[ 1 ] );

                        factor = new Complex( 0.5 );

                        arg = ( ( FunctionVariable ) ( ( Polynomial ) arg ).Var ).Var;

                        xf = FunctionVariable.Create( "log", arg );
                    }

                    try
                    {
                        var re = arg.RealPart();
                        var im = arg.ImagPart();

                        if ( im != Symbol.ZERO )
                        {
                            bool min_im = minus( im );

                            if ( min_im )
                            {
                                im = -im;
                            }

                            var a1 = new SqrtExpand().SymEval( arg * arg.Conj() );

                            var a = FunctionVariable.Create( "log", a1 ) / Symbol.TWO;

                            var b1 = SymEval( re / im );

                            var b = FunctionVariable.Create( "atan", b1 ) * Symbol.IONE;

                            xf = min_im ? a + b : a - b;

                            var pi2 = Symbol.PI * Symbol.IONE / Symbol.TWO;

                            xf = min_im ? xf - pi2 : xf + pi2;
                        }
                    }
                    catch ( SymbolicException )
                    {
                    }

                    if ( xf != null )
                    {
                        xf = xf * factor + sum;
                    }
                }

                if ( xf == null )
                {
                    return x.Map( this );
                }

                Algebraic r = Symbol.ZERO;

                for ( int i = xp.Coeffs.Length - 1; i > 0; i-- )
                {
                    r = ( r + SymEval( xp[ i ] ) ) * xf;
                }

                if ( xp.Coeffs.Length > 0 )
                {
                    r = r + SymEval( xp[ 0 ] );
                }

                return r;
            }

            return x.Map( this );
        }

        internal static bool minus( Algebraic x )
        {
            if ( x is Symbol )
            {
                return ( ( Symbol ) x ).Smaller( Symbol.ZERO );
            }

            if ( x is Polynomial )
            {
                return minus( ( ( Polynomial ) x )[ ( ( Polynomial ) x ).Degree() ] );
            }

            if ( x is Rational )
            {
                var a = minus( ( ( Rational ) x ).nom );
                var b = minus( ( ( Rational ) x ).den );

                return ( a && !b ) || ( !a && b );
            }

            throw new SymbolicException( "minus not implemented for " + x );
        }
    }

    internal class LambdaSIN : LambdaAlgebraic
    {
        public LambdaSIN()
        {
            diffrule = "cos(x)";
            intrule = "-cos(x)";
            trigrule = "1/(2*i)*(exp(i*x)-exp(-i*x))";
        }

        internal override Symbol PreEval( Symbol x )
        {
            var z = x.ToComplex();

            if ( z.Im == 0.0 )
            {
                return new Complex( Math.sin( z.Re ) );
            }

            return ( Symbol ) evalx( trigrule, z );
        }

        internal override Algebraic SymEval( Algebraic x )
        {
            if ( x.Equals( Symbol.ZERO ) )
            {
                return Symbol.ZERO;
            }

            return null;
        }
    }

    internal class LambdaCOS : LambdaAlgebraic
    {
        public LambdaCOS()
        {
            diffrule = "-sin(x)";
            intrule = "sin(x)";
            trigrule = "1/2 *(exp(i*x)+exp(-i*x))";
        }
        internal override Symbol PreEval( Symbol x )
        {
            Complex z = x.ToComplex();
            if ( z.Im == 0.0 )
            {
                return new Complex( Math.cos( z.Re ) );
            }
            return ( Symbol ) evalx( trigrule, z );
        }
        internal override Algebraic SymEval( Algebraic x )
        {
            if ( x.Equals( Symbol.ZERO ) )
            {
                return Symbol.ONE;
            }
            return null;
        }
    }
    internal class LambdaTAN : LambdaAlgebraic
    {
        public LambdaTAN()
        {
            diffrule = "1/(cos(x))^2";
            intrule = "-log(cos(x))";
            trigrule = "-i*(exp(i*x)-exp(-i*x))/(exp(i*x)+exp(-i*x))";
        }
        internal override Symbol PreEval( Symbol x )
        {
            Complex z = x.ToComplex();
            if ( z.Im == 0.0 )
            {
                return new Complex( Math.tan( z.Re ) );
            }
            return ( Symbol ) evalx( trigrule, z );
        }
        internal override Algebraic SymEval( Algebraic x )
        {
            if ( x.Equals( Symbol.ZERO ) )
            {
                return Symbol.ZERO;
            }
            return null;
        }
    }
    internal class LambdaATAN : LambdaAlgebraic
    {
        public LambdaATAN()
        {
            diffrule = "1/(1+x^2)";
            intrule = "x*atan(x)-1/2*log(1+x^2)";
            trigrule = "-i/2*log((1+i*x)/(1-i*x))";
        }

        internal override Symbol PreEval( Symbol x )
        {
            var z = x.ToComplex();

            if ( z.Im == 0.0 )
            {
                return new Complex( Math.atan( z.Re ) );
            }

            return ( Symbol ) evalx( trigrule, z );
        }

        internal override Algebraic SymEval( Algebraic x )
        {
            return Equals( x, Symbol.ZERO ) ? Symbol.ZERO : null;
        }
    }

    internal class LambdaASIN : LambdaAlgebraic
    {
        public LambdaASIN()
        {
            diffrule = "1/sqrt(1-x^2)";
            intrule = "x*asin(x)+sqrt(1-x^2)";
            trigrule = "-i*log(i*x+i*sqrt(1-x^2))";
        }

        internal override Symbol PreEval( Symbol x )
        {
            var z = x.ToComplex();

            if ( z.Im == 0.0 )
            {
                return new Complex( Math.asin( z.Re ) );
            }

            return ( Symbol ) evalx( trigrule, z );
        }
    }

    internal class LambdaACOS : LambdaAlgebraic
    {
        public LambdaACOS()
        {
            diffrule = "-1/sqrt(1-x^2)";
            intrule = "x*acos(x)-sqrt(1-x^2)";
            trigrule = "-i*log(x+i*sqrt(1-x^2))";
        }

        internal override Symbol PreEval( Symbol x )
        {
            var z = x.ToComplex();

            if ( z.Im == 0.0 )
            {
                return new Complex( Math.acos( z.Re ) );
            }

            return ( Symbol ) evalx( trigrule, z );
        }
    }

    internal class LambdaATAN2 : LambdaAlgebraic
    {
        internal override Symbol PreEval( Symbol x )
        {
            return null;
        }

        internal override Algebraic SymEval( Algebraic x )
        {
            throw new SymbolicException( "Usage: ATAN2(y,x)." );
        }

        internal override Algebraic SymEval( Algebraic[] x )
        {
            throw new SymbolicException( "Usage: ATAN2(y,x)." );
        }

        internal override Algebraic SymEval( Algebraic x, Algebraic y )
        {
            if ( y is Complex && !y.IsComplex() && x is Complex && !x.IsComplex() )
            {
                return new Complex( Math.atan2( ( ( Complex ) y ).Re, ( ( Complex ) x ).Re ) );
            }

            if ( Symbol.ZERO != x )
            {
                return FunctionVariable.Create( "atan", y / x ) + FunctionVariable.Create( "sign", y ) * ( Symbol.ONE - FunctionVariable.Create( "sign", x ) ) * Symbol.PI / Symbol.TWO;
            }
            else
            {
                return ( FunctionVariable.Create( "sign", y ) * Symbol.PI ) / Symbol.TWO;
            }
        }
    }
}
