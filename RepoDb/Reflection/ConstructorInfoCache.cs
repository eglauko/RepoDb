﻿using System;
using System.Collections.Generic;
using System.Reflection;

namespace RepoDb.Reflection
{
    /// <summary>
    /// A cacher for a System.Reflection.ConstructorInfo.
    /// </summary>
    public static class ConstructorInfoCache
    {
        private static readonly IDictionary<Type, ConstructorInfo> _cache = new Dictionary<Type, ConstructorInfo>();

        /// <summary>
        /// Gets a System.Reflection.ConstructorInfo object of the defined type.
        /// </summary>
        /// <param name="type">The Type where to create a constructor info.</param>
        /// <returns>A System.Reflection.ConstructorInfo object of the defined type.</returns>
        public static ConstructorInfo Get(Type type)
        {
            if (!_cache.ContainsKey(type))
            {
                _cache.Add(type, ReflectionFactory.GetConstructor(type));
            }
            return _cache[type];
        }

        /// <summary>
        /// Gets a System.Reflection.ConstructorInfo object of the defined type.
        /// </summary>
        /// <param name="type">The System.Type object  where to create a constructor info.</param>
        /// <param name="contructorTypes">The arguments of the constructor.</param>
        /// <returns>A System.Reflection.ConstructorInfo object of the defined type.</returns>
        public static ConstructorInfo Get(Type type, params Type[] constructorTypes)
        {
            if (!_cache.ContainsKey(type))
            {
                _cache.Add(type, ReflectionFactory.GetConstructor(type, constructorTypes));
            }
            return _cache[type];
        }
    }
}