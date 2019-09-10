using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class P
{
    public static string T<E>( E x , bool removeTypeTag = false , int numUntilSeparateArray = 99999999 )
    {
#if DEBUG_CONSOLE || UNITY_EDITOR || true
        return _T( x , 0 , removeTypeTag , numUntilSeparateArray );
#else
		return "";
#endif
    }

    public static void L<E>( E x , string desc = null , bool removeTypeTag = false ,
        int numUntilSeparateArray = 99999999 )
    {
#if DEBUG_CONSOLE || UNITY_EDITO || true
        if ( desc == null )
            Debug.Log( T( x , removeTypeTag , numUntilSeparateArray ) );
        else
            Debug.Log( desc + " : " + T( x , removeTypeTag , numUntilSeparateArray ) );
#endif
    }

#if DEBUG_CONSOLE || UNITY_EDITOR || true
    private static string _T<E>( E x , int recursiveLv , bool removeTypeTag , int numUntilSeparateArray )
    {
        var desc = "";

        if ( x == null )
        {
            desc = "(null) ";
            return desc;
        }

        var type = x.GetType();
        var colorCodeStart = "<color=green>";
        var colorCodeEnd = "</color>";
        var startTag = removeTypeTag ? "" : colorCodeStart + x.GetType() + ": " + colorCodeEnd;

        if ( type.IsArray )
        {
            desc = startTag;
            var a = (Array) (object) x;
            desc += _ToString( a , removeTypeTag , numUntilSeparateArray , ++recursiveLv );
        }

        else if ( type.GetMethod( "ToArray" ) != null )
        {
            desc = startTag;
            var methodInfo = type.GetMethod( "ToArray" );
            var a = (Array) methodInfo.Invoke( x , null );
            desc += _ToString( a , removeTypeTag , numUntilSeparateArray , ++recursiveLv );
        }

        else if ( x is IDictionary )
        {
            var d = (IDictionary) x;
            var keyStr = new List<string>( d.Keys.Count );
            var valueStr = new List<string>( d.Keys.Count );
            foreach ( var item in d.Keys )
                keyStr.Add( T( item ) );

            foreach ( var item in d.Values )
                valueStr.Add( T( item ) );

            for ( var i = 0 ; i < keyStr.Count ; i++ )
                desc += "[Pair" + i + "]{ " + keyStr[i] + ", " + valueStr[i] + " } " + "\n";
        }

        else if ( type == typeof(Vector3) )
        {
            var v3 = (Vector3) (object) x;
            if ( recursiveLv == 0 ) desc = startTag; //colorCodeStart + "Vector3: " + colorCodeEnd;
            desc += "(" + v3.x + ", " + v3.y + ", " + v3.z + ") ";
        }

        else if ( type == typeof(Vector2) )
        {
            Vector3 v2 = (Vector2) (object) x;
            if ( recursiveLv == 0 ) desc = startTag; //colorCodeStart + "Vector2: " + colorCodeEnd;
            desc += "(" + v2.x + ", " + v2.y + ") ";
        }

        else if ( type == typeof(Vector4) )
        {
            var v4 = (Vector4) (object) x;
            if ( recursiveLv == 0 ) desc = startTag; //colorCodeStart + "Vector2: " + colorCodeEnd;
            desc += "(" + v4.x + ", " + v4.y + ", " + v4.z + ", " + v4.w + ") ";
        }

        else if ( type == typeof(Keyframe) )
        {
            var keyframe = (Keyframe) (object) x;
            if ( recursiveLv == 0 ) desc = startTag; //colorCodeStart + "keyframe: " + colorCodeEnd;
            desc += "(" + keyframe.time + ", " + keyframe.value + ", " + keyframe.inTangent + ", " +
                    keyframe.outTangent + ") ";
        }

        else if ( type == typeof(Bounds) )
        {
            var bound = (Bounds) (object) x;
            if ( recursiveLv == 0 ) desc = startTag; //colorCodeStart + "keyframe: " + colorCodeEnd;
            desc += "(min: " + bound.min.x + ", " + bound.min.y + ", " + bound.min.z + ", max: " + bound.max.x + ", " +
                    bound.max.y + ", " + bound.max.z + " ) ";
        }

        else
        {
            desc = x + " ";
        }

        return desc;
    }

    private static string _ToString( Array a , bool removeTypeTag , int numUntilSeparateArray , int recursiveLv )
    {
        var desc = "";
        var isItemArray = false;

        foreach ( var o in a )
        {
            if (
                o != null &&
                (o.GetType().IsArray || o.GetType().GetMethod( "ToArray" ) != null)
            )
                isItemArray = true;
        }

        var counter = 0;

        foreach ( var o in a )
        {
            if ( isItemArray )
            {
                desc += '\n';
                for ( var i = 0 ; i < recursiveLv ; i++ )
                    desc += '\t';
            }

            desc += _T( o , recursiveLv , removeTypeTag , numUntilSeparateArray );

            if ( ++counter == numUntilSeparateArray )
            {
                desc += '\n';
                counter = 0;
            }
        }

        return desc;
    }
#endif
}