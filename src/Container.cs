/*
# Container for Arbitrary Implementations

To register and retrieve interface implementations at runtime we need a helper
class to track them. Classes that use runtime type information to manage
interface implementations are typically called _containers_ in 
[IoC](https://en.wikipedia.org/wiki/Inversion_of_control) frameworks. We 
define a simple container class that helps us dynamically create objects which
implement the IArbitrary interface. The class we write supports any generic
interface, but we use it exclusively with `IArbitrary<T>`.
*/
namespace LinqCheck
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;

	public class Container
	{
		/*
		## Stored Data
		The runtime type handle is of the generic interface is stored in the
		following field.
		*/
		private Type _interface;
		/*
		The types implementing the interface are stored in a dictionary. The 
		keys of the dictionary are the interface's generic type parameters, and 
		the	values are the types which implement the specific interface 
		instance.
		*/
		private Dictionary<Type, Type> _types;
		/*
		An alternative method to register an implementation is to use a
		singleton object instead of a type. The dictionary below stores 
		those. As above, its keys are the interface's generic type parameters, 
		but its values objects which implement the interface instance.
		*/
		private Dictionary<Type, object> _objects;
		/*
		## Constructor
		The constructor takes the runtime type handle of the generic interface
		and stores in the `_interface` field. It checks that the type object 
		corresponds to an interface, and that interface is generic. The 
		constructor also creates the two dictionaries introduced above.
		*/
		public Container (Type intf)
		{
			if (!(intf.IsInterface && intf.IsGenericType))
				throw new ArgumentException (
					"Given type must be generic interface.", "intf");
			_interface = intf;
			_types = new Dictionary<Type, Type> ();
			_objects = new Dictionary<Type, object> ();
		}
		/*
		## Registering Implementations
		We provide several ways of registering implementations. The most 
		sweeping version takes an assembly and registers all types defined in 
		it.
		*/
		public void Register (Assembly assembly)
		{
			foreach (var type in assembly.GetTypes ())
				Register (type);
		}
		/*
		The following overload is used to register a type that implements one
		or more interface instances. It throws an exception if the type is 
		already registered, or if the implementation does have a default
		constructor.
		*/
		public void Register (Type type)
		{
			if (_types.ContainsKey (type))
				throw new ArgumentException (string.Format (
					"Type {0} is already registered", type));
			if (!type.GetConstructors ().Any (x => x.GetParameters ().Length == 0))
				throw new ArgumentException (string.Format (
					"Type {0} does not contain a default constructor", type));
			foreach (var argType in ArgumentTypes (type))
				_types.Add (GenericDef (argType), GenericDef (type));
		}
		/*
		The last overload takes an object, finds out which interface instances 
		it implements, and adds all of them to the `_objects` dictionary. 
		*/
		public void Register (object obj)
		{
			var type = obj.GetType ();
			if (_objects.ContainsKey (type))
				throw new ArgumentException (string.Format (
					"Type {0} is already registered", type));

			foreach (var argType in ArgumentTypes (type))
				_objects.Add (argType, obj);
		}
		/*
		## Retrieving an Implementation
		Depending on how the implementation is registered the method below 
		will either locate or construct it as needed. If an implementation
		is not registered the `ImplementingType` helper method will throw an
		exception.
		*/
		public object GetImplementation (Type argType)
		{
			if (!_objects.TryGetValue (argType, out object result))
			{
				Type implementingType = ImplementingType (argType);
				result = Activator.CreateInstance (implementingType);
				_objects.Add (argType, result);
			}
			return result;
		}
		/*
		## Helper Methods
		Rest of the methods of the Container class are private helpers, which 
		simplify the work of the methods defined above. The `GenericDef` method
		takes a type handle and returns its generic definition. For arrays this
		is the [System.Array](https://msdn.microsoft.com/en-us/library/system.array(v=vs.110).aspx) 
		class. For instances of generic types the methods returns the type 
		handle where none of the generic parameters is instantiated. If the 
		type given is not generic at all, it is returned back as-is.
		*/
		private static Type GenericDef (Type type)
		{
			return type.IsArray ? 
				typeof (Array) :
				type.IsGenericType ? 
					type.GetGenericTypeDefinition () : 
					type;
		}
		/*
		Given a type handle, we need to find out which instances of the generic
		interface it implements. The `ArgumentTypes` method does this and 
		returns an IEnumerable of type parameter instantiations.
		*/
		private IEnumerable<Type> ArgumentTypes (Type type)
		{
			return from i in type.GetInterfaces ()
				   where i.IsGenericType && 
						 i.GetGenericTypeDefinition () == _interface
				   select i.GetGenericArguments ().First ();
		}
		/*
		The last helper method finds the type in the `_types` dictionary that
		implements the interface for the specified type parameter, and 
		constructs an instance of that class. It needs to create generic 
		classes and classes that provide implementation for arrays in a 
		different way.
		*/
		private Type ImplementingType (Type argType)
		{
			var generic = GenericDef (argType);

			if (!_types.TryGetValue (generic, out Type implementor))
				throw new InvalidOperationException (string.Format (
					"Could not find a generator for {0}, please register one.",
					generic.Name));

			return generic == typeof (Array) ?
				implementor.MakeGenericType (argType.GetElementType ()) :
				implementor.IsGenericTypeDefinition ?
					implementor.MakeGenericType (argType.GetGenericArguments ()) :
					implementor;
		}
	}
}
