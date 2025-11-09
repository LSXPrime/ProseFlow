using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace ProseFlow.UI.Converters;

/// <summary>
/// A multi-value converter that compares two values for equality.
/// It returns a specified "true" value if they are equal, and a "false" value otherwise.
/// </summary>
public class EqualityToValuesConverter : IMultiValueConverter
{
    /// <summary>
    /// Converts multiple input values to a single output value based on an equality check.
    /// </summary>
    /// <param name="values">
    /// The list of values to convert. Expects at least two values for comparison.
    /// values[0]: The first value to compare.
    /// values[1]: The second value to compare.
    /// </param>
    /// <param name="targetType">The type of the binding target property.</param>
    /// <param name="parameter">
    /// A string containing the true and false results, separated by a pipe '|'.
    /// Example: "TrueValue|FalseValue".
    /// </param>
    /// <param name="culture">The culture to use in the converter.</param>
    /// <returns>The true result if the first two values are equal; otherwise, the false result.</returns>
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // Ensure we have the right number of inputs.
        if (values.Count < 2)
            return AvaloniaProperty.UnsetValue;

        // Ensure the parameter is a valid string.
        if (parameter is not string paramString)
            return AvaloniaProperty.UnsetValue;

        // Parse the true/false results from the parameter.
        var parts = paramString.Split('|');
        if (parts.Length < 2)
            return AvaloniaProperty.UnsetValue;
        
        var trueResult = parts[0];
        var falseResult = parts[1];
        
        var value1 = values[0];
        var value2 = values[1];

        // Perform the equality check. object.Equals is safe for nulls.
        var areEqual = Equals(value1, value2);

        return areEqual ? trueResult : falseResult;
    }
}