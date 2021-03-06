﻿using FreeSql.Internal;
using FreeSql.Internal.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace FreeSql.SqlServer.Curd {

	class SqlServerSelect<T1> : FreeSql.Internal.CommonProvider.Select1Provider<T1> where T1 : class {

		internal static string ToSqlStatic(CommonUtils _commonUtils, string _select, string field, StringBuilder _join, StringBuilder _where, string _groupby, string _having, string _orderby, int _skip, int _limit, List<SelectTableInfo> _tables, IFreeSql _orm)
			=> (_commonUtils as SqlServerUtils).IsSelectRowNumber ?
			ToSqlStaticRowNumber(_commonUtils, _select, field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, _orm) :
			ToSqlStaticOffsetFetchNext(_commonUtils, _select, field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, _orm);

		#region SqlServer 2005 row_number
		internal static string ToSqlStaticRowNumber(CommonUtils _commonUtils, string _select, string field, StringBuilder _join, StringBuilder _where, string _groupby, string _having, string _orderby, int _skip, int _limit, List<SelectTableInfo> _tables, IFreeSql _orm) {
			if (_orm.CodeFirst.IsAutoSyncStructure)
				_orm.CodeFirst.SyncStructure(_tables.Select(a => a.Table.Type).ToArray());

			var sb = new StringBuilder();
			sb.Append(_select);
			if (_limit > 0) sb.Append("TOP ").Append(_skip + _limit).Append(" ");
			sb.Append(field);
			if (_skip > 0) {
				if (string.IsNullOrEmpty(_orderby)) {
					var pktb = _tables.Where(a => a.Table.Primarys.Any()).FirstOrDefault();
					if (pktb != null) _orderby = string.Concat(" \r\nORDER BY ", pktb.Alias, ".", _commonUtils.QuoteSqlName(pktb?.Table.Primarys.First().Attribute.Name));
					else _orderby = string.Concat(" \r\nORDER BY ", _tables.First().Alias, ".", _commonUtils.QuoteSqlName(_tables.First().Table.Columns.First().Value.Attribute.Name));
				}
				sb.Append(", ROW_NUMBER() OVER(").Append(_orderby).Append(") AS __rownum__");
			}
			sb.Append(" \r\nFROM ");
			var tbsjoin = _tables.Where(a => a.Type != SelectTableInfoType.From).ToArray();
			var tbsfrom = _tables.Where(a => a.Type == SelectTableInfoType.From).ToArray();
			for (var a = 0; a < tbsfrom.Length; a++) {
				sb.Append(_commonUtils.QuoteSqlName(tbsfrom[a].Table.DbName)).Append(" ").Append(tbsfrom[a].Alias);
				if (tbsjoin.Length > 0) {
					//如果存在 join 查询，则处理 from t1, t2 改为 from t1 inner join t2 on 1 = 1
					for (var b = 1; b < tbsfrom.Length; b++)
						sb.Append(" \r\nLEFT JOIN ").Append(_commonUtils.QuoteSqlName(tbsfrom[b].Table.DbName)).Append(" ").Append(tbsfrom[b].Alias).Append(" ON 1 = 1");
					break;
				}
				if (a < tbsfrom.Length - 1) sb.Append(", ");
			}
			foreach (var tb in tbsjoin) {
				if (tb.Type == SelectTableInfoType.Parent) continue;
				switch (tb.Type) {
					case SelectTableInfoType.LeftJoin:
						sb.Append(" \r\nLEFT JOIN ");
						break;
					case SelectTableInfoType.InnerJoin:
						sb.Append(" \r\nINNER JOIN ");
						break;
					case SelectTableInfoType.RightJoin:
						sb.Append(" \r\nRIGHT JOIN ");
						break;
				}
				sb.Append(_commonUtils.QuoteSqlName(tb.Table.DbName)).Append(" ").Append(tb.Alias).Append(" ON ").Append(tb.On);
			}
			if (_join.Length > 0) sb.Append(_join);

			var sbqf = new StringBuilder();
			foreach (var tb in _tables) {
				if (tb.Type == SelectTableInfoType.Parent) continue;
				if (string.IsNullOrEmpty(tb.Table.SelectFilter) == false)
					sbqf.Append(" AND (").Append(tb.Table.SelectFilter.Replace("a.", $"{tb.Alias}.")).Append(")");
			}
			if (_where.Length > 0) {
				sb.Append(" \r\nWHERE ").Append(_where.ToString().Substring(5));
				if (sbqf.Length > 0) sb.Append(sbqf.ToString());
			} else {
				if (sbqf.Length > 0) sb.Append(" \r\nWHERE ").Append(sbqf.Remove(0, 5));
			}
			if (string.IsNullOrEmpty(_groupby) == false) {
				sb.Append(_groupby);
				if (string.IsNullOrEmpty(_having) == false)
					sb.Append(" \r\nHAVING ").Append(_having.Substring(5));
			}
			if (_skip <= 0)
				sb.Append(_orderby);
			else
				sb.Insert(0, "WITH t AS ( ").Append(" ) SELECT t.* FROM t where __rownum__ > ").Append(_skip);

			return sb.ToString();
		}
		#endregion

		#region SqlServer 2012+ offset feach next
		internal static string ToSqlStaticOffsetFetchNext(CommonUtils _commonUtils, string _select, string field, StringBuilder _join, StringBuilder _where, string _groupby, string _having, string _orderby, int _skip, int _limit, List<SelectTableInfo> _tables, IFreeSql _orm) {
			if (_orm.CodeFirst.IsAutoSyncStructure)
				_orm.CodeFirst.SyncStructure(_tables.Select(a => a.Table.Type).ToArray());

			var sb = new StringBuilder();
			sb.Append(_select);
			if (_skip <= 0 && _limit > 0) sb.Append("TOP ").Append(_limit).Append(" ");
			sb.Append(field);
			sb.Append(" \r\nFROM ");
			var tbsjoin = _tables.Where(a => a.Type != SelectTableInfoType.From).ToArray();
			var tbsfrom = _tables.Where(a => a.Type == SelectTableInfoType.From).ToArray();
			for (var a = 0; a < tbsfrom.Length; a++) {
				sb.Append(_commonUtils.QuoteSqlName(tbsfrom[a].Table.DbName)).Append(" ").Append(tbsfrom[a].Alias);
				if (tbsjoin.Length > 0) {
					//如果存在 join 查询，则处理 from t1, t2 改为 from t1 inner join t2 on 1 = 1
					for (var b = 1; b < tbsfrom.Length; b++)
						sb.Append(" \r\nLEFT JOIN ").Append(_commonUtils.QuoteSqlName(tbsfrom[b].Table.DbName)).Append(" ").Append(tbsfrom[b].Alias).Append(" ON 1 = 1");
					break;
				}
				if (a < tbsfrom.Length - 1) sb.Append(", ");
			}
			foreach (var tb in tbsjoin) {
				if (tb.Type == SelectTableInfoType.Parent) continue;
				switch (tb.Type) {
					case SelectTableInfoType.LeftJoin:
						sb.Append(" \r\nLEFT JOIN ");
						break;
					case SelectTableInfoType.InnerJoin:
						sb.Append(" \r\nINNER JOIN ");
						break;
					case SelectTableInfoType.RightJoin:
						sb.Append(" \r\nRIGHT JOIN ");
						break;
				}
				sb.Append(_commonUtils.QuoteSqlName(tb.Table.DbName)).Append(" ").Append(tb.Alias).Append(" ON ").Append(tb.On);
			}
			if (_join.Length > 0) sb.Append(_join);

			var sbqf = new StringBuilder();
			foreach (var tb in _tables) {
				if (tb.Type == SelectTableInfoType.Parent) continue;
				if (string.IsNullOrEmpty(tb.Table.SelectFilter) == false)
					sbqf.Append(" AND (").Append(tb.Table.SelectFilter.Replace("a.", $"{tb.Alias}.")).Append(")");
			}
			if (_where.Length > 0) {
				sb.Append(" \r\nWHERE ").Append(_where.ToString().Substring(5));
				if (sbqf.Length > 0) sb.Append(sbqf.ToString());
			} else {
				if (sbqf.Length > 0) sb.Append(" \r\nWHERE ").Append(sbqf.Remove(0, 5));
			}
			if (string.IsNullOrEmpty(_groupby) == false) {
				sb.Append(_groupby);
				if (string.IsNullOrEmpty(_having) == false)
					sb.Append(" \r\nHAVING ").Append(_having.Substring(5));
			}
			if (_skip > 0) {
				if (string.IsNullOrEmpty(_orderby)) {
					var pktb = _tables.Where(a => a.Table.Primarys.Any()).FirstOrDefault();
					if (pktb != null) _orderby = string.Concat(" \r\nORDER BY ", pktb.Alias, ".", _commonUtils.QuoteSqlName(pktb?.Table.Primarys.First().Attribute.Name));
					else _orderby = string.Concat(" \r\nORDER BY ", _tables.First().Alias, ".", _commonUtils.QuoteSqlName(_tables.First().Table.Columns.First().Value.Attribute.Name));
				}
				sb.Append(_orderby).Append($" \r\nOFFSET {_skip} ROW");
				if (_limit > 0) sb.Append($" \r\nFETCH NEXT {_limit} ROW ONLY");
			} else {
				sb.Append(_orderby);
			}

			return sb.ToString();
		}
		#endregion

		public SqlServerSelect(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression, object dywhere) : base(orm, commonUtils, commonExpression, dywhere) { }
		public override ISelect<T1, T2, T3> From<T2, T3>(Expression<Func<ISelectFromExpression<T1>, T2, T3, ISelectFromExpression<T1>>> exp) { this.InternalFrom(exp?.Body); var ret = new SqlServerSelect<T1, T2, T3>(_orm, _commonUtils, _commonExpression, null); SqlServerSelect<T1>.CopyData(this, ret); return ret; }
		public override ISelect<T1, T2, T3, T4> From<T2, T3, T4>(Expression<Func<ISelectFromExpression<T1>, T2, T3, T4, ISelectFromExpression<T1>>> exp) { this.InternalFrom(exp?.Body); var ret = new SqlServerSelect<T1, T2, T3, T4>(_orm, _commonUtils, _commonExpression, null); SqlServerSelect<T1>.CopyData(this, ret); return ret; }
		public override ISelect<T1, T2, T3, T4, T5> From<T2, T3, T4, T5>(Expression<Func<ISelectFromExpression<T1>, T2, T3, T4, T5, ISelectFromExpression<T1>>> exp) { this.InternalFrom(exp?.Body); var ret = new SqlServerSelect<T1, T2, T3, T4, T5>(_orm, _commonUtils, _commonExpression, null); SqlServerSelect<T1>.CopyData(this, ret); return ret; }
		public override ISelect<T1, T2, T3, T4, T5, T6> From<T2, T3, T4, T5, T6>(Expression<Func<ISelectFromExpression<T1>, T2, T3, T4, T5, T6, ISelectFromExpression<T1>>> exp) { this.InternalFrom(exp?.Body); var ret = new SqlServerSelect<T1, T2, T3, T4, T5, T6>(_orm, _commonUtils, _commonExpression, null); SqlServerSelect<T1>.CopyData(this, ret); return ret; }
		public override ISelect<T1, T2, T3, T4, T5, T6, T7> From<T2, T3, T4, T5, T6, T7>(Expression<Func<ISelectFromExpression<T1>, T2, T3, T4, T5, T6, T7, ISelectFromExpression<T1>>> exp) { this.InternalFrom(exp?.Body); var ret = new SqlServerSelect<T1, T2, T3, T4, T5, T6, T7>(_orm, _commonUtils, _commonExpression, null); SqlServerSelect<T1>.CopyData(this, ret); return ret; }
		public override ISelect<T1, T2, T3, T4, T5, T6, T7, T8> From<T2, T3, T4, T5, T6, T7, T8>(Expression<Func<ISelectFromExpression<T1>, T2, T3, T4, T5, T6, T7, T8, ISelectFromExpression<T1>>> exp) { this.InternalFrom(exp?.Body); var ret = new SqlServerSelect<T1, T2, T3, T4, T5, T6, T7, T8>(_orm, _commonUtils, _commonExpression, null); SqlServerSelect<T1>.CopyData(this, ret); return ret; }
		public override ISelect<T1, T2, T3, T4, T5, T6, T7, T8, T9> From<T2, T3, T4, T5, T6, T7, T8, T9>(Expression<Func<ISelectFromExpression<T1>, T2, T3, T4, T5, T6, T7, T8, T9, ISelectFromExpression<T1>>> exp) { this.InternalFrom(exp?.Body); var ret = new SqlServerSelect<T1, T2, T3, T4, T5, T6, T7, T8, T9>(_orm, _commonUtils, _commonExpression, null); SqlServerSelect<T1>.CopyData(this, ret); return ret; }
		public override ISelect<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> From<T2, T3, T4, T5, T6, T7, T8, T9, T10>(Expression<Func<ISelectFromExpression<T1>, T2, T3, T4, T5, T6, T7, T8, T9, T10, ISelectFromExpression<T1>>> exp) { this.InternalFrom(exp?.Body); var ret = new SqlServerSelect<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(_orm, _commonUtils, _commonExpression, null); SqlServerSelect<T1>.CopyData(this, ret); return ret; }
		public override string ToSql(string field = null) => ToSqlStatic(_commonUtils, _select, field ?? this.GetAllFieldExpressionTree().Field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, _orm);
	}
	//class SqlServerSelect<T1, T2> : FreeSql.Internal.CommonProvider.Select2Provider<T1, T2> where T1 : class where T2 : class {
	//	public SqlServerSelect(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression, object dywhere) : base(orm, commonUtils, commonExpression, dywhere) { }
	//	public override string ToSql(string field = null) => SqlServerSelect<T1>.ToSqlStatic(_commonUtils, _select, field ?? this.GetAllField().field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, _orm);
	//}
	class SqlServerSelect<T1, T2, T3> : FreeSql.Internal.CommonProvider.Select3Provider<T1, T2, T3> where T1 : class where T2 : class where T3 : class {
		public SqlServerSelect(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression, object dywhere) : base(orm, commonUtils, commonExpression, dywhere) { }
		public override string ToSql(string field = null) => SqlServerSelect<T1>.ToSqlStatic(_commonUtils, _select, field ?? this.GetAllFieldExpressionTree().Field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, _orm);
	}
	class SqlServerSelect<T1, T2, T3, T4> : FreeSql.Internal.CommonProvider.Select4Provider<T1, T2, T3, T4> where T1 : class where T2 : class where T3 : class where T4 : class {
		public SqlServerSelect(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression, object dywhere) : base(orm, commonUtils, commonExpression, dywhere) { }
		public override string ToSql(string field = null) => SqlServerSelect<T1>.ToSqlStatic(_commonUtils, _select, field ?? this.GetAllFieldExpressionTree().Field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, _orm);
	}
	class SqlServerSelect<T1, T2, T3, T4, T5> : FreeSql.Internal.CommonProvider.Select5Provider<T1, T2, T3, T4, T5> where T1 : class where T2 : class where T3 : class where T4 : class where T5 : class {
		public SqlServerSelect(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression, object dywhere) : base(orm, commonUtils, commonExpression, dywhere) { }
		public override string ToSql(string field = null) => SqlServerSelect<T1>.ToSqlStatic(_commonUtils, _select, field ?? this.GetAllFieldExpressionTree().Field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, _orm);
	}
	class SqlServerSelect<T1, T2, T3, T4, T5, T6> : FreeSql.Internal.CommonProvider.Select6Provider<T1, T2, T3, T4, T5, T6> where T1 : class where T2 : class where T3 : class where T4 : class where T5 : class where T6 : class {
		public SqlServerSelect(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression, object dywhere) : base(orm, commonUtils, commonExpression, dywhere) { }
		public override string ToSql(string field = null) => SqlServerSelect<T1>.ToSqlStatic(_commonUtils, _select, field ?? this.GetAllFieldExpressionTree().Field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, _orm);
	}
	class SqlServerSelect<T1, T2, T3, T4, T5, T6, T7> : FreeSql.Internal.CommonProvider.Select7Provider<T1, T2, T3, T4, T5, T6, T7> where T1 : class where T2 : class where T3 : class where T4 : class where T5 : class where T6 : class where T7 : class {
		public SqlServerSelect(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression, object dywhere) : base(orm, commonUtils, commonExpression, dywhere) { }
		public override string ToSql(string field = null) => SqlServerSelect<T1>.ToSqlStatic(_commonUtils, _select, field ?? this.GetAllFieldExpressionTree().Field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, _orm);
	}
	class SqlServerSelect<T1, T2, T3, T4, T5, T6, T7, T8> : FreeSql.Internal.CommonProvider.Select8Provider<T1, T2, T3, T4, T5, T6, T7, T8> where T1 : class where T2 : class where T3 : class where T4 : class where T5 : class where T6 : class where T7 : class where T8 : class {
		public SqlServerSelect(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression, object dywhere) : base(orm, commonUtils, commonExpression, dywhere) { }
		public override string ToSql(string field = null) => SqlServerSelect<T1>.ToSqlStatic(_commonUtils, _select, field ?? this.GetAllFieldExpressionTree().Field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, _orm);
	}
	class SqlServerSelect<T1, T2, T3, T4, T5, T6, T7, T8, T9> : FreeSql.Internal.CommonProvider.Select9Provider<T1, T2, T3, T4, T5, T6, T7, T8, T9> where T1 : class where T2 : class where T3 : class where T4 : class where T5 : class where T6 : class where T7 : class where T8 : class where T9 : class {
		public SqlServerSelect(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression, object dywhere) : base(orm, commonUtils, commonExpression, dywhere) { }
		public override string ToSql(string field = null) => SqlServerSelect<T1>.ToSqlStatic(_commonUtils, _select, field ?? this.GetAllFieldExpressionTree().Field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, _orm);
	}
	class SqlServerSelect<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> : FreeSql.Internal.CommonProvider.Select10Provider<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> where T1 : class where T2 : class where T3 : class where T4 : class where T5 : class where T6 : class where T7 : class where T8 : class where T9 : class where T10 : class {
		public SqlServerSelect(IFreeSql orm, CommonUtils commonUtils, CommonExpression commonExpression, object dywhere) : base(orm, commonUtils, commonExpression, dywhere) { }
		public override string ToSql(string field = null) => SqlServerSelect<T1>.ToSqlStatic(_commonUtils, _select, field ?? this.GetAllFieldExpressionTree().Field, _join, _where, _groupby, _having, _orderby, _skip, _limit, _tables, _orm);
	}
}
