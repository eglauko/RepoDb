﻿using System;
using System.Collections.Generic;
using System.Linq;
using RepoDb.Enumerations;
using RepoDb.Extensions;
using RepoDb.Attributes;
using RepoDb.Exceptions;

namespace RepoDb
{
    /// <summary>
    /// A widely-used object for defining the groupings for the query expressions. This object is used by most of the repository operations
    /// to define the filtering and query expressions for the actual execution.
    /// </summary>
    public class QueryGroup : IEquatable<QueryGroup>
    {
        private bool m_isFixed = false;
        private int? m_hashCode = null;

        /// <summary>
        /// Creates a new instance of <see cref="QueryGroup"/> object.
        /// </summary>
        /// <param name="queryFields">The list of fields to be grouped for the query expressions.</param>
        /// <param name="queryGroups">The child query groups to be grouped for the query expressions.</param>
        /// <param name="conjunction">The conjunction to be used for every group seperation.</param>
        public QueryGroup(IEnumerable<QueryField> queryFields, IEnumerable<QueryGroup> queryGroups = null, Conjunction conjunction = Conjunction.And)
        {
            Conjunction = conjunction;
            QueryFields = queryFields;
            QueryGroups = queryGroups;
        }

        /// <summary>
        /// Gets the conjunction used by this object..
        /// </summary>
        public Conjunction Conjunction { get; }

        /// <summary>
        /// Gets the list of fields being grouped by this object.
        /// </summary>
        public IEnumerable<QueryField> QueryFields { get; }

        /// <summary>
        /// Gets the list of child query groups being grouped by this object.
        /// </summary>
        public IEnumerable<QueryGroup> QueryGroups { get; }

        /// <summary>
        /// Force to append prefixes on the bound parameter objects.
        /// </summary>
        internal void AppendParametersPrefix()
        {
            QueryFields?
                .ToList()
                .ForEach(queryField => queryField.AppendParameterPrefix());
        }

        /// <summary>
        /// Gets the text value of <see cref="TextAttribute"/> implemented at the <see cref="Conjunction"/> property value of this instance.
        /// </summary>
        /// <returns>A string instance containing the value of the <see cref="TextAttribute"/> text property.</returns>
        public string GetConjunctionText()
        {
            var textAttribute = typeof(Conjunction)
                .GetMembers()
                .First(member => member.Name.ToLower() == Conjunction.ToString().ToLower())
                .GetCustomAttribute<TextAttribute>();
            return textAttribute.Text;
        }

        /// <summary>
        /// Gets the stringified query expression format of the current instance. A formatted string for field-operation-parameter will be
        /// conjuncted by the value of the <see cref="Conjunction"/> property.
        /// </summary>
        /// <returns>A stringified formatted-text of the current instance.</returns>
        public string GetString()
        {
            var groupList = new List<string>();
            var conjunction = GetConjunctionText();
            if (QueryFields != null && QueryFields.Any())
            {
                var fieldList = new List<string>();
                QueryFields.ToList().ForEach(queryField =>
                {
                    fieldList.Add(queryField.AsFieldAndParameter());
                });
                groupList.Add(fieldList.Join($" {conjunction} "));
            }
            if (QueryGroups != null && QueryGroups.Any())
            {
                var fieldList = new List<string>();
                QueryGroups.ToList().ForEach(queryGroup =>
                {
                    fieldList.Add(queryGroup.GetString());
                });
                groupList.Add(fieldList.Join($" {conjunction} "));
            }
            return $"({groupList.Join($" {conjunction} ")})";
        }

        /// <summary>
        /// Gets all the child query groups associated on the current instance.
        /// </summary>
        /// <returns>An enumerable list of child query groups.</returns>
        public IEnumerable<QueryField> GetAllQueryFields()
        {
            var traverse = (Action<QueryGroup>)null;
            var queryFields = new List<QueryField>();
            traverse = new Action<QueryGroup>((queryGroup) =>
            {
                if (queryGroup.QueryFields != null && queryGroup.QueryFields.Any())
                {
                    queryFields.AddRange(queryGroup.QueryFields);
                }
                if (queryGroup.QueryGroups != null && queryGroup.QueryGroups.Any())
                {
                    queryGroup.QueryGroups.ToList().ForEach(qg =>
                    {
                        traverse(qg);
                    });
                }
            });
            traverse(this);
            return queryFields;
        }

        /// <summary>
        /// Fixes the parameter naming convention. This method must be called atleast once prior the actual operation execution.
        /// Please note that every repository operation itself is calling this method before the actual execution.
        /// </summary>
        /// <returns>The current instance.</returns>
        internal QueryGroup FixParameters()
        {
            if (m_isFixed)
            {
                return this;
            }
            var firstList = GetAllQueryFields()?
                .OrderBy(queryField => queryField.Parameter.Name)
                .ToList();
            if (firstList != null && firstList.Any())
            {
                var secondList = new List<QueryField>(firstList);
                for (var i = 0; i < firstList.Count; i++)
                {
                    var queryField = firstList[i];
                    for (var c = 0; c < secondList.Count; c++)
                    {
                        var cQueryField = secondList[c];
                        if (ReferenceEquals(queryField, cQueryField))
                        {
                            continue;
                        }
                        if (queryField.Field.Equals(cQueryField.Field))
                        {
                            var fieldValue = cQueryField.Parameter;
                            fieldValue.Name = $"{cQueryField.Parameter.Name}_{c}";
                        }
                    }
                    secondList.RemoveAll(qf => qf.Field.Equals(qf.Field));
                }
            }
            m_isFixed = true;
            return this;
        }

        // Static Methods

        /// <summary>
        /// This method is used to parse the customized query tree expression. This method expects a dynamic object and converts it to the actual
        /// <see cref="QueryGroup"/> that defines the query tree expression.
        /// </summary>
        /// <param name="obj">
        /// A dynamic query tree expression to be parsed.
        /// Example:
        /// var expression = new { Conjunction = Conjunction.And, Company = "Microsoft",
        /// FirstName = new { Operation = Operation.Like, Value = "An%" },
        /// UpdatedDate = new { Operation = Operation.LessThan, Value = DateTime.UtcNow.Date }}
        /// </param>
        /// <returns>An instance of the <see cref="QueryGroup"/> object that contains the parsed query expression.</returns>
        public static QueryGroup Parse(object obj)
        {
            // Cannot further optimize and shortify this method, this one works like a charm for now.

            // Check for value
            if (obj == null)
            {
                throw new ArgumentNullException($"Parameter '{StringConstant.Obj.ToLower()}' cannot be null.");
            }

            // Variables
            var queryFields = new List<QueryField>();
            var queryGroups = new List<QueryGroup>();
            var conjunction = Conjunction.And;

            // Iterate every property
            var objectProperties = obj.GetType().GetProperties();
            objectProperties.ToList().ForEach(property =>
            {
                var fieldName = property.Name;

                // Identify the fields
                if (string.Equals(fieldName, StringConstant.Conjunction, StringComparison.CurrentCultureIgnoreCase))
                {
                    // Throws an exception if conjunction is not a conjunction type
                    if (property.PropertyType != typeof(Conjunction))
                    {
                        throw new InvalidQueryExpressionException($"Conjunction field must be of type {typeof(Conjunction).FullName}.");
                    }

                    // Conjunction
                    conjunction = (Conjunction)property.GetValue(obj);
                }
                else if (string.Equals(fieldName, StringConstant.QueryGroups, StringComparison.CurrentCultureIgnoreCase))
                {
                    // Child QueryGroups
                    var value = property.GetValue(obj);
                    if (value is Array)
                    {
                        ((Array)value).AsEnumerable().ToList().ForEach(item =>
                        {
                            queryGroups.Add(Parse(item));
                        });
                    }
                    else
                    {
                        queryGroups.Add(Parse(value));
                    }
                }
                else
                {
                    // Other pre-defined fields
                    var value = property.GetValue(obj);
                    var type = value?.GetType();

                    if (type?.IsGenericType == false || value == null)
                    {
                        // Most likely, (Field.Name = <value|null>)
                        queryFields.Add(new QueryField(fieldName, value));
                    }
                    else
                    {
                        // Another dynamic object type, get the 'Operation' property
                        var properties = type?.GetProperties();
                        var operationProperty = properties?.FirstOrDefault(p => p.Name.ToLower() == StringConstant.Operation.ToLower());

                        // The property 'Operation' must always be present
                        if (operationProperty == null)
                        {
                            throw new InvalidQueryExpressionException($"The 'Operation' property must be present for field '{property.Name}'.");
                        }

                        // The property operatoin must be of type 'RepoDb.Enumerations.Operation'
                        if (operationProperty.PropertyType != typeof(Operation))
                        {
                            throw new InvalidQueryExpressionException($"The 'Operation' property for field '{property.Name}' must be of type '{typeof(Operation).FullName}'.");
                        }

                        // The 'Value' property must always be present
                        var valueProperty = properties?.FirstOrDefault(p => p.Name.ToLower() == StringConstant.Value.ToLower());

                        // Check for the 'Value' property
                        if (valueProperty == null)
                        {
                            throw new InvalidQueryExpressionException($"The 'Value' property for dynamic type query must be present at field '{property.Name}'.");
                        }

                        // Get the 'Operation' and the 'Value' value
                        var operation = (Operation)operationProperty.GetValue(value);
                        value = valueProperty.GetValue(value);

                        // For other operation, the 'Value' property must be present
                        if (value == null && (operation != Operation.Equal && operation != Operation.NotEqual))
                        {
                            throw new InvalidQueryExpressionException($"The value property '{valueProperty.Name}' must not be null.");
                        }

                        // Identify the 'Operation' and parse the correct value
                        if ((operation == Operation.Equal || operation == Operation.NotEqual) && value == null)
                        {
                            // Most likely, new { Field.Name = { Operation = Operation.<Equal|NotEqual>, Value = (object)null } }
                            // It should be (IS NULL) or (IS NOT NULL) in SQL Statement
                            queryFields.Add(new QueryField(fieldName, operation, value));
                        }
                        else if (operation == Operation.All || operation == Operation.Any)
                        {
                            // Special case: All (AND), Any (OR)
                            if (value.GetType().IsArray)
                            {
                                var childQueryGroupFields = new List<QueryField>();
                                ((Array)value).AsEnumerable().ToList().ForEach(underlyingValue =>
                                {
                                    childQueryGroupFields.Add(QueryField.Parse(fieldName, underlyingValue));
                                });
                                var queryGroup = new QueryGroup(childQueryGroupFields, null, operation == Operation.All ? Conjunction.And : Conjunction.Or);
                                queryGroups.Add(queryGroup);
                            }
                            else
                            {
                                queryFields.Add(QueryField.Parse(fieldName, value));
                            }
                        }
                        else
                        {

                            if (operation == Operation.Between || operation == Operation.NotBetween)
                            {
                                // Special case: (Field.Name = new { Operation = Operation.<Between|NotBetween>, Value = new [] { value1, value2 })
                                ValidateBetweenOperations(fieldName, operation, value);
                            }
                            else if (operation == Operation.In || operation == Operation.NotIn)
                            {
                                // Special case: (Field.Name = new { Operation = Operation.<In|NotIn>, Value = new [] { value1, value2 })
                                ValidateInOperations(fieldName, operation, value);
                            }
                            else
                            {
                                // Other Operations
                                ValidateOtherOperations(fieldName, operation, value);
                            }

                            // Add the field values
                            queryFields.Add(new QueryField(fieldName, operation, value));
                        }
                    }
                }
            });

            // Return
            return new QueryGroup(queryFields, queryGroups, conjunction).FixParameters();
        }

        private static void ValidateBetweenOperations(string fieldName, Operation operation, object value)
        {
            var valid = false;

            // Make sure it is an Array
            if (value?.GetType().IsArray == true)
            {
                var values = ((Array)value)
                    .AsEnumerable()
                    .ToList();

                // The items must only be 2. There should be no NULL and no generic types
                if (values.Count == 2)
                {
                    valid = !values.Any(v => v == null || v?.GetType().IsGenericType == true);
                }

                // All type must be the same
                if (valid)
                {
                    var type = values.First().GetType();
                    values.ForEach(v =>
                    {
                        if (valid == false) return;
                        valid = v?.GetType() == type;
                    });
                }
            }

            // Throw an error if not valid
            if (valid == false)
            {
                throw new InvalidQueryExpressionException($"Invalid value for field '{fieldName}' for operation '{operation.ToString()}'. The value must be an array of 2 values with identitcal data types.");
            }
        }

        private static void ValidateInOperations(string fieldName, Operation operation, object value)
        {
            var valid = false;

            // Make sure it is an array
            if (value?.GetType().IsArray == true)
            {
                var values = ((Array)value)
                    .AsEnumerable()
                    .ToList();

                // Make sure there is not NULL and no generic types
                valid = !values.Any(v => v == null || v?.GetType().IsGenericType == true);

                // All type must be the same
                if (valid)
                {
                    var type = values.First().GetType();
                    values.ForEach(v =>
                    {
                        if (valid == false) return;
                        valid = v?.GetType() == type;
                    });
                }
            }

            // Throw an error if not valid
            if (valid == false)
            {
                throw new InvalidQueryExpressionException($"Invalid value for field '{fieldName}' for operation '{operation.ToString()}'. The value must be an array values with identitcal data types.");
            }
        }

        private static void ValidateOtherOperations(string fieldName, Operation operation, object value)
        {
            var valid = false;

            // Special for Equal and NonEqual
            if ((operation == Operation.Equal || operation == Operation.NotEqual) && value == null)
            {
                // Most likely new QueryField("Field.Name", null) or new { FieldName = (object)null }.
                // The SQL must be (@FieldName IS <NOT> NULL)
                valid = true;
            }
            else
            {
                // Must not be a generic
                valid = (value?.GetType().IsGenericType == false);
            }

            // Throw an error if not valid
            if (valid == false)
            {
                throw new InvalidQueryExpressionException($"Invalid value for field '{fieldName}' for operation '{operation.ToString()}'.");
            }
        }

        // Equality and comparers

        /// <summary>
        /// Returns the hashcode for this <see cref="QueryGroup"/>.
        /// </summary>
        /// <returns>The hashcode value.</returns>
        public override int GetHashCode()
        {
            // Make sure to check if this is already taken
            if (!ReferenceEquals(null, m_hashCode))
            {
                return m_hashCode.Value;
            }

            // Set the default value (should not be nullable for better performance)
            var hashCode = 0;

            // Iterates the child query field
            if (!ReferenceEquals(null, QueryFields))
            {
                QueryFields.ToList().ForEach(queryField =>
                {
                    hashCode += queryField.GetHashCode();
                });
            }

            // Iterates the child query groups
            if (!ReferenceEquals(null, QueryGroups))
            {
                QueryGroups.ToList().ForEach(queryGroup =>
                {
                    hashCode += queryGroup.GetHashCode();
                });
            }

            // Set with conjunction
            hashCode += Conjunction.GetHashCode();

            // Set back the hashcode value
            m_hashCode = hashCode;

            // Return the actual hash code
            return hashCode;
        }

        /// <summary>
        /// Compares the <see cref="QueryGroup"/> object equality against the given target object.
        /// </summary>
        /// <param name="obj">The object to be compared to the current object.</param>
        /// <returns>True if the instances are equals.</returns>
        public override bool Equals(object obj)
        {
            return GetHashCode() == obj?.GetHashCode();
        }

        /// <summary>
        /// Compares the <see cref="QueryGroup"/> object equality against the given target object.
        /// </summary>
        /// <param name="other">The object to be compared to the current object.</param>
        /// <returns>True if the instances are equal.</returns>
        public bool Equals(QueryGroup other)
        {
            return GetHashCode() == other?.GetHashCode();
        }

        /// <summary>
        /// Compares the equality of the two <see cref="QueryGroup"/> objects.
        /// </summary>
        /// <param name="objA">The first <see cref="QueryGroup"/> object.</param>
        /// <param name="objB">The second <see cref="QueryGroup"/> object.</param>
        /// <returns>True if the instances are equal.</returns>
        public static bool operator ==(QueryGroup objA, QueryGroup objB)
        {
            if (ReferenceEquals(null, objA))
            {
                return ReferenceEquals(null, objB);
            }
            return objA?.GetHashCode() == objB?.GetHashCode();
        }

        /// <summary>
        /// Compares the inequality of the two <see cref="QueryGroup"/> objects.
        /// </summary>
        /// <param name="objA">The first <see cref="QueryGroup"/> object.</param>
        /// <param name="objB">The second <see cref="QueryGroup"/> object.</param>
        /// <returns>True if the instances are not equal.</returns>
        public static bool operator !=(QueryGroup objA, QueryGroup objB)
        {
            return (objA == objB) == false;
        }
    }
}